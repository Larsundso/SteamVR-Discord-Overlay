using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VRDiscordOverlay.Discord.Models;

namespace VRDiscordOverlay.Discord;

public class DiscordRpcClient : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource _cts = new();
    private readonly string _clientId;
    private readonly string _clientSecret;
    private string? _accessToken;
    private string? _subscribedChannelId;
    private int _nonceCounter;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject?>> _pendingRequests = new();
    private readonly HashSet<string> _subscribedTextChannels = new();

    private const int OP_HANDSHAKE = 0;
    private const int OP_FRAME = 1;
    private const int OP_CLOSE = 2;

    public event Action<RpcVoiceStateData>? OnVoiceStateCreate;
    public event Action<RpcVoiceStateData>? OnVoiceStateUpdate;
    public event Action<RpcVoiceStateData>? OnVoiceStateDelete;
    public event Action<string>? OnSpeakingStart;
    public event Action<string>? OnSpeakingStop;
    public event Action<JObject>? OnNotificationCreate;
    public event Action<string>? OnVoiceConnectionStatus;
    public event Action<RpcChannelData?>? OnVoiceChannelSelect;
    public event Action? OnReady;
    public event Action? OnDisconnected;
    public event Action<string>? OnError;
    public event Action<JObject>? OnMessageCreate;
    public event Action<JObject>? OnMessageUpdate;
    public event Action<JObject>? OnMessageDelete;
    public event Action<bool, bool>? OnVoiceSettingsUpdate;

    public string? AccessToken => _accessToken;
    public bool IsConnected => _pipe?.IsConnected ?? false;

    public DiscordRpcClient(string clientId, string clientSecret)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public int ConnectedPipe { get; private set; } = -1;

    public async Task ConnectAsync(string? savedAccessToken = null, int pipeNumber = -1, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _accessToken = savedAccessToken;

        int start = pipeNumber >= 0 ? pipeNumber : 0;
        int end = pipeNumber >= 0 ? pipeNumber : 9;

        for (int i = start; i <= end; i++)
        {
            try
            {
                var pipeName = $"discord-ipc-{i}";
                _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await _pipe.ConnectAsync(1000, _cts.Token);
                ConnectedPipe = i;
                ConsoleUI.Log($"Connected to Discord (pipe {i})");

                var handshake = new JObject
                {
                    ["v"] = 1,
                    ["client_id"] = _clientId
                };
                await WriteFrameAsync(OP_HANDSHAKE, handshake.ToString(Formatting.None));

                _ = ReceiveLoopAsync();
                return;
            }
            catch
            {
                _pipe?.Dispose();
                _pipe = null;
            }
        }

        throw new Exception(pipeNumber >= 0
            ? $"Could not connect to Discord on pipe {pipeNumber}"
            : "Could not find Discord. Is it running?");
    }

    private string NextNonce() => $"nonce-{Interlocked.Increment(ref _nonceCounter)}";

    private async Task WriteFrameAsync(int opcode, string payload)
    {
        if (_pipe == null || !_pipe.IsConnected) return;

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var header = new byte[8];
        BitConverter.GetBytes(opcode).CopyTo(header, 0);
        BitConverter.GetBytes(payloadBytes.Length).CopyTo(header, 4);

        await _writeLock.WaitAsync(_cts.Token);
        try
        {
            await _pipe.WriteAsync(header, _cts.Token);
            await _pipe.WriteAsync(payloadBytes, _cts.Token);
            await _pipe.FlushAsync(_cts.Token);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SendCommandAsync(string command, JObject? args = null, string? evt = null)
    {
        var frame = new JObject
        {
            ["cmd"] = command,
            ["nonce"] = NextNonce()
        };
        if (args != null) frame["args"] = args;
        if (evt != null) frame["evt"] = evt;

        var json = frame.ToString(Formatting.None);
        await WriteFrameAsync(OP_FRAME, json);
    }

    public async Task<JObject?> SendCommandWithResponseAsync(string command, JObject? args = null, int timeoutMs = 5000)
    {
        var nonce = NextNonce();
        var tcs = new TaskCompletionSource<JObject?>();
        _pendingRequests[nonce] = tcs;

        var frame = new JObject { ["cmd"] = command, ["nonce"] = nonce };
        if (args != null) frame["args"] = args;
        await WriteFrameAsync(OP_FRAME, frame.ToString(Formatting.None));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        timeoutCts.CancelAfter(timeoutMs);
        var reg = timeoutCts.Token.Register(() =>
        {
            if (_pendingRequests.TryRemove(nonce, out var removed))
                removed.TrySetResult(null);
        });

        try { return await tcs.Task; }
        finally { await reg.DisposeAsync(); }
    }

    public async Task ForceReauthAsync()
    {
        _accessToken = null;
        await AuthorizeAsync();
    }

    public async Task AuthorizeAsync()
    {
        await SendCommandAsync("AUTHORIZE", new JObject
        {
            ["client_id"] = _clientId,
            ["scopes"] = new JArray("rpc", "rpc.voice.read", "rpc.voice.write", "rpc.notifications.read", "messages.read", "identify", "guilds", "guilds.members.read")
        });
    }

    public async Task AuthenticateAsync(string accessToken)
    {
        _accessToken = accessToken;
        await SendCommandAsync("AUTHENTICATE", new JObject
        {
            ["access_token"] = accessToken
        });
    }

    public async Task SubscribeToVoiceChannel(string channelId)
    {
        if (_subscribedChannelId != null)
        {
            await UnsubscribeFromVoiceChannel(_subscribedChannelId);
        }

        _subscribedChannelId = channelId;
        var args = new JObject { ["channel_id"] = channelId };

        await SendCommandAsync("SUBSCRIBE", args, "VOICE_STATE_CREATE");
        await SendCommandAsync("SUBSCRIBE", args, "VOICE_STATE_UPDATE");
        await SendCommandAsync("SUBSCRIBE", args, "VOICE_STATE_DELETE");
        await SendCommandAsync("SUBSCRIBE", args, "SPEAKING_START");
        await SendCommandAsync("SUBSCRIBE", args, "SPEAKING_STOP");
    }

    private async Task UnsubscribeFromVoiceChannel(string channelId)
    {
        var args = new JObject { ["channel_id"] = channelId };
        await SendCommandAsync("UNSUBSCRIBE", args, "VOICE_STATE_CREATE");
        await SendCommandAsync("UNSUBSCRIBE", args, "VOICE_STATE_UPDATE");
        await SendCommandAsync("UNSUBSCRIBE", args, "VOICE_STATE_DELETE");
        await SendCommandAsync("UNSUBSCRIBE", args, "SPEAKING_START");
        await SendCommandAsync("UNSUBSCRIBE", args, "SPEAKING_STOP");
    }

    public async Task SubscribeToVoiceChannelSelect()
    {
        await SendCommandAsync("SUBSCRIBE", null, "VOICE_CHANNEL_SELECT");
    }

    public async Task GetSelectedVoiceChannel()
    {
        await SendCommandAsync("GET_SELECTED_VOICE_CHANNEL");
    }

    private async Task ReceiveLoopAsync()
    {
        var headerBuf = new byte[8];

        try
        {
            while (_pipe?.IsConnected == true && !_cts.Token.IsCancellationRequested)
            {
                int headerRead = 0;
                while (headerRead < 8)
                {
                    int n = await _pipe.ReadAsync(headerBuf.AsMemory(headerRead, 8 - headerRead), _cts.Token);
                    if (n == 0)
                    {
                        ConsoleUI.Log("Discord connection closed");
                        return;
                    }
                    headerRead += n;
                }

                int opcode = BitConverter.ToInt32(headerBuf, 0);
                int length = BitConverter.ToInt32(headerBuf, 4);

                var payloadBuf = new byte[length];
                int payloadRead = 0;
                while (payloadRead < length)
                {
                    int n = await _pipe.ReadAsync(payloadBuf.AsMemory(payloadRead, length - payloadRead), _cts.Token);
                    if (n == 0)
                    {
                        ConsoleUI.Log("Discord connection lost");
                        return;
                    }
                    payloadRead += n;
                }

                var json = Encoding.UTF8.GetString(payloadBuf);

                switch (opcode)
                {
                    case OP_FRAME:
                    case OP_HANDSHAKE:
                        HandleMessage(json);
                        break;
                    case OP_CLOSE:
                        ConsoleUI.Log("Discord closed the connection");
                        return;
                    case 3:
                        await WriteFrameAsync(4, json);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException)
        {
            ConsoleUI.Log("Lost connection to Discord");
        }
        catch (Exception)
        {
            ConsoleUI.Log("Connection to Discord failed unexpectedly");
        }
        finally
        {
            OnDisconnected?.Invoke();
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            var frame = JsonConvert.DeserializeObject<RpcFrame>(json);
            if (frame == null) return;

            if (frame.Nonce != null && _pendingRequests.TryRemove(frame.Nonce, out var pendingTcs))
                pendingTcs.TrySetResult(frame.Data);

            switch (frame.Command)
            {
                case "DISPATCH":
                    HandleDispatch(frame);
                    break;
                case "AUTHORIZE":
                    _ = SafeAsync(() => HandleAuthorizeAsync(frame));
                    break;
                case "AUTHENTICATE":
                    _ = SafeAsync(() => HandleAuthenticateAsync(frame));
                    break;
                case "GET_SELECTED_VOICE_CHANNEL":
                    HandleGetSelectedVoiceChannel(frame);
                    break;
                case "SUBSCRIBE":
                    if (frame.Event == "ERROR")
                    {
                        ConsoleUI.Log($"Subscription error: {frame.Data?["message"]}");
                        var failedChannel = frame.Args?["channel_id"]?.ToString();
                        if (failedChannel != null)
                            lock (_subscribedTextChannels) _subscribedTextChannels.Remove(failedChannel);
                    }
                    break;
                case "UNSUBSCRIBE":
                    break;
            }
        }
        catch
        {
            ConsoleUI.Log("Failed to process a Discord message");
        }
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { OnError?.Invoke(ex.Message); }
    }

    private void HandleDispatch(RpcFrame frame)
    {
        switch (frame.Event)
        {
            case "READY":
                ConsoleUI.Log("Authenticating...");
                _ = SafeAsync(HandleReadyAsync);
                break;
            case "VOICE_STATE_CREATE":
                if (frame.Data != null)
                {
                    var data = frame.Data.ToObject<RpcVoiceStateData>();
                    if (data != null) OnVoiceStateCreate?.Invoke(data);
                }
                break;
            case "VOICE_STATE_UPDATE":
                if (frame.Data != null)
                {
                    var data = frame.Data.ToObject<RpcVoiceStateData>();
                    if (data != null) OnVoiceStateUpdate?.Invoke(data);
                }
                break;
            case "VOICE_STATE_DELETE":
                if (frame.Data != null)
                {
                    var data = frame.Data.ToObject<RpcVoiceStateData>();
                    if (data != null) OnVoiceStateDelete?.Invoke(data);
                }
                break;
            case "SPEAKING_START":
                if (frame.Data != null)
                {
                    var data = frame.Data.ToObject<RpcSpeakingData>();
                    if (data != null) OnSpeakingStart?.Invoke(data.UserId);
                }
                break;
            case "SPEAKING_STOP":
                if (frame.Data != null)
                {
                    var data = frame.Data.ToObject<RpcSpeakingData>();
                    if (data != null) OnSpeakingStop?.Invoke(data.UserId);
                }
                break;
            case "NOTIFICATION_CREATE":
                if (frame.Data != null)
                    OnNotificationCreate?.Invoke(frame.Data);
                break;
            case "VOICE_CONNECTION_STATUS":
                if (frame.Data != null)
                {
                    var state = frame.Data["state"]?.ToString() ?? "DISCONNECTED";
                    OnVoiceConnectionStatus?.Invoke(state);
                }
                break;
            case "VOICE_CHANNEL_SELECT":
                _ = SafeAsync(() => HandleVoiceChannelSelectAsync(frame));
                break;
            case "MESSAGE_CREATE":
                if (frame.Data != null) OnMessageCreate?.Invoke(frame.Data);
                break;
            case "MESSAGE_UPDATE":
                if (frame.Data != null) OnMessageUpdate?.Invoke(frame.Data);
                break;
            case "MESSAGE_DELETE":
                if (frame.Data != null) OnMessageDelete?.Invoke(frame.Data);
                break;
            case "VOICE_SETTINGS_UPDATE":
                if (frame.Data != null)
                {
                    bool m = frame.Data["mute"]?.Value<bool>() ?? false;
                    bool d = frame.Data["deaf"]?.Value<bool>() ?? false;
                    OnVoiceSettingsUpdate?.Invoke(m, d);
                }
                break;
        }
    }

    private async Task HandleReadyAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken))
            await AuthenticateAsync(_accessToken);
        else
            await AuthorizeAsync();
    }

    private async Task HandleAuthorizeAsync(RpcFrame frame)
    {
        if (frame.Data == null) return;
        var code = frame.Data["code"]?.ToString();
        if (string.IsNullOrEmpty(code))
        {
            OnError?.Invoke("Authorization failed");
            return;
        }

        var token = await ExchangeCodeForToken(code);
        if (token != null)
            await AuthenticateAsync(token);
    }

    private async Task HandleAuthenticateAsync(RpcFrame frame)
    {
        if (frame.Event == "ERROR")
        {
            ConsoleUI.Log("Session expired, requesting new authorization...");
            _accessToken = null;
            await AuthorizeAsync();
            return;
        }

        if (frame.Data == null)
        {
            ConsoleUI.Log("Authentication issue, requesting new authorization...");
            _accessToken = null;
            await AuthorizeAsync();
            return;
        }

        ConsoleUI.Log("Authenticated! Listening for voice activity.");
        OnReady?.Invoke();

        await SubscribeToVoiceChannelSelect();
        await SendCommandAsync("SUBSCRIBE", null, "NOTIFICATION_CREATE");
        await SendCommandAsync("SUBSCRIBE", null, "VOICE_CONNECTION_STATUS");
        await SendCommandAsync("SUBSCRIBE", null, "VOICE_SETTINGS_UPDATE");
        await GetSelectedVoiceChannel();
    }

    private void HandleGetSelectedVoiceChannel(RpcFrame frame)
    {
        if (frame.Data == null || !frame.Data.HasValues)
        {
            OnVoiceChannelSelect?.Invoke(null);
            return;
        }

        var channelData = frame.Data.ToObject<RpcChannelData>();
        OnVoiceChannelSelect?.Invoke(channelData);
    }

    private async Task HandleVoiceChannelSelectAsync(RpcFrame frame)
    {
        if (frame.Data == null) return;
        var channelId = frame.Data["channel_id"]?.ToString();

        if (string.IsNullOrEmpty(channelId))
        {
            if (_subscribedChannelId != null)
            {
                await UnsubscribeFromVoiceChannel(_subscribedChannelId);
                _subscribedChannelId = null;
            }
            OnVoiceChannelSelect?.Invoke(null);
        }
        else
        {
            await GetSelectedVoiceChannel();
        }
    }

    private async Task<string?> ExchangeCodeForToken(string code)
    {
        try
        {
            using var http = new HttpClient();
            var response = await http.PostAsync("https://discord.com/api/oauth2/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = "http://localhost"
                }));

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                OnError?.Invoke($"Authorization token exchange failed");
                return null;
            }

            var data = JObject.Parse(json);
            return data["access_token"]?.ToString();
        }
        catch
        {
            OnError?.Invoke("Could not exchange authorization code");
            return null;
        }
    }

    public async Task<(bool muted, bool deafened)> GetVoiceSettingsAsync()
    {
        var result = await SendCommandWithResponseAsync("GET_VOICE_SETTINGS");
        if (result == null) return (false, false);
        return (result["mute"]?.Value<bool>() ?? false, result["deaf"]?.Value<bool>() ?? false);
    }

    public async Task SetVoiceSettingsAsync(bool? mute = null, bool? deaf = null)
    {
        var args = new JObject();
        if (mute.HasValue) args["mute"] = mute.Value;
        if (deaf.HasValue) args["deaf"] = deaf.Value;
        await SendCommandAsync("SET_VOICE_SETTINGS", args);
    }

    public async Task<JObject?> GetGuildsAsync()
    {
        return await SendCommandWithResponseAsync("GET_GUILDS");
    }

    public async Task<JObject?> GetChannelsAsync(string guildId)
    {
        return await SendCommandWithResponseAsync("GET_CHANNELS", new JObject { ["guild_id"] = guildId });
    }

    public async Task SubscribeToTextChannel(string channelId)
    {
        var args = new JObject { ["channel_id"] = channelId };
        await SendCommandAsync("SUBSCRIBE", args, "MESSAGE_CREATE");
        await SendCommandAsync("SUBSCRIBE", args, "MESSAGE_UPDATE");
        await SendCommandAsync("SUBSCRIBE", args, "MESSAGE_DELETE");
        lock (_subscribedTextChannels) _subscribedTextChannels.Add(channelId);
    }

    public async Task UnsubscribeFromTextChannel(string channelId)
    {
        var args = new JObject { ["channel_id"] = channelId };
        await SendCommandAsync("UNSUBSCRIBE", args, "MESSAGE_CREATE");
        await SendCommandAsync("UNSUBSCRIBE", args, "MESSAGE_UPDATE");
        await SendCommandAsync("UNSUBSCRIBE", args, "MESSAGE_DELETE");
        lock (_subscribedTextChannels) _subscribedTextChannels.Remove(channelId);
    }

    public IReadOnlyCollection<string> GetSubscribedTextChannels()
    {
        lock (_subscribedTextChannels) return _subscribedTextChannels.ToArray();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _pipe?.Dispose();
    }
}
