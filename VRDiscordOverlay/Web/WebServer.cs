using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VRDiscordOverlay.Config;

namespace VRDiscordOverlay.Web;

public class WebServer
{
    private WebApplication? _app;
    private readonly AppSettings _settings;
    private readonly List<WebSocket> _clients = new();
    private readonly ConcurrentDictionary<WebSocket, SemaphoreSlim> _sendLocks = new();
    private readonly List<string> _logBuffer = new();
    private readonly object _lock = new();
    private object? _lastState;
    private const int MaxLogBuffer = 200;
    public int Port { get; private set; }

    public event Action<string, string?>? OnCommand;

    public Func<Task<JObject?>>? GetGuilds { get; set; }
    public Func<string, Task<JObject?>>? GetChannels { get; set; }
    public Func<string, Task>? SubscribeChannel { get; set; }
    public Func<string, Task>? UnsubscribeChannel { get; set; }
    public Func<IReadOnlyCollection<string>>? GetSubscribedChannels { get; set; }
    public Action<string, string, string>? RegisterChannelInfo { get; set; }

    public WebServer(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        Port = 39039;
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://localhost:{Port}");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        _app = builder.Build();

        _app.UseWebSockets();

        _app.MapGet("/", () => Results.Content(DashboardHtml.Page, "text/html"));

        _app.MapGet("/api/settings", () => Results.Text(
            JsonConvert.SerializeObject(_settings), "application/json"));

        _app.MapPost("/api/settings", async (HttpContext ctx) =>
        {
            var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            var patch = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            if (patch == null) return Results.BadRequest();

            foreach (var kv in patch)
            {
                var prop = typeof(AppSettings).GetProperty(kv.Key);
                if (prop == null || !prop.CanWrite) continue;

                var val = Convert.ChangeType(
                    kv.Value is Newtonsoft.Json.Linq.JToken jt ? jt.ToObject(prop.PropertyType) : kv.Value,
                    prop.PropertyType);
                prop.SetValue(_settings, val);
            }

            SettingsManager.Save(_settings);
            OnCommand?.Invoke("settings_changed", null);
            return Results.Ok();
        });

        _app.MapPost("/api/command/{name}", async (string name, HttpContext ctx) =>
        {
            string? arg = null;
            if (ctx.Request.ContentLength > 0)
                arg = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            OnCommand?.Invoke(name, arg);
            return Results.Ok();
        });

        _app.MapGet("/api/guilds", async () =>
        {
            if (GetGuilds == null) return Results.StatusCode(503);
            var data = await GetGuilds();
            return Results.Text(data?.ToString() ?? "{}", "application/json");
        });

        _app.MapGet("/api/guilds/{guildId}/channels", async (string guildId) =>
        {
            if (GetChannels == null) return Results.StatusCode(503);
            var data = await GetChannels(guildId);
            return Results.Text(data?.ToString() ?? "{}", "application/json");
        });

        _app.MapPost("/api/channels/{channelId}/subscribe", async (string channelId, HttpContext ctx) =>
        {
            if (SubscribeChannel == null) return Results.StatusCode(503);
            await SubscribeChannel(channelId);
            var name = ctx.Request.Query["name"].FirstOrDefault() ?? channelId;
            var guild = ctx.Request.Query["guild"].FirstOrDefault() ?? "";
            ConsoleUI.Log($"Subscribed to #{name}" + (guild != "" ? $" ({guild})" : ""));
            RegisterChannelInfo?.Invoke(channelId, name, guild);
            _settings.SavedSubscriptions.Remove(channelId);
            _settings.SavedSubscriptions[channelId] = guild != "" ? $"{name}|{guild}" : name;
            if (_settings.SavedSubscriptions.Count > 5)
                _settings.SavedSubscriptions.Remove(_settings.SavedSubscriptions.Keys.First());
            SettingsManager.Save(_settings);
            return Results.Ok();
        });

        _app.MapPost("/api/channels/{channelId}/unsubscribe", async (string channelId, HttpContext ctx) =>
        {
            if (UnsubscribeChannel == null) return Results.StatusCode(503);
            await UnsubscribeChannel(channelId);
            var name = ctx.Request.Query["name"].FirstOrDefault() ?? channelId;
            ConsoleUI.Log($"Unsubscribed from #{name}");
            _settings.SavedSubscriptions.Remove(channelId);
            SettingsManager.Save(_settings);
            return Results.Ok();
        });

        _app.MapGet("/api/subscriptions", () =>
        {
            var ids = GetSubscribedChannels?.Invoke() ?? Array.Empty<string>();
            var result = new Dictionary<string, string>();
            foreach (var id in ids)
                result[id] = _settings.SavedSubscriptions.TryGetValue(id, out var n) ? n : id;
            return Results.Text(JsonConvert.SerializeObject(result), "application/json");
        });

        _app.Map("/ws", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }

            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            var sendLock = new SemaphoreSlim(1, 1);
            _sendLocks[ws] = sendLock;

            string[] logs;
            object? state;
            lock (_lock)
            {
                logs = _logBuffer.ToArray();
                state = _lastState;
            }

            foreach (var log in logs)
            {
                var logJson = JsonConvert.SerializeObject(new { type = "log", message = log });
                await SendToClientAsync(ws, Encoding.UTF8.GetBytes(logJson));
            }

            if (state != null)
            {
                var stateJson = JsonConvert.SerializeObject(new { type = "state", data = state });
                await SendToClientAsync(ws, Encoding.UTF8.GetBytes(stateJson));
            }

            lock (_lock) { _clients.Add(ws); }

            var buf = new byte[1024];
            try
            {
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(buf, ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch { }
            finally
            {
                lock (_lock) { _clients.Remove(ws); }
                _sendLocks.TryRemove(ws, out _);
            }
        });

        await _app.StartAsync(ct);
    }

    public void BroadcastLog(string message)
    {
        lock (_lock)
        {
            _logBuffer.Add(message);
            if (_logBuffer.Count > MaxLogBuffer)
                _logBuffer.RemoveAt(0);
        }
        var json = JsonConvert.SerializeObject(new { type = "log", message });
        Broadcast(json);
    }

    public void BroadcastMessage(string messageType, object data)
    {
        var json = JsonConvert.SerializeObject(new { type = messageType, data });
        Broadcast(json);
    }

    public void BroadcastState(object state)
    {
        _lastState = state;
        var json = JsonConvert.SerializeObject(new { type = "state", data = state });
        Broadcast(json);
    }

    private void Broadcast(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        WebSocket[] clients;
        lock (_lock) { clients = _clients.ToArray(); }

        foreach (var ws in clients)
        {
            if (ws.State == WebSocketState.Open)
                _ = SendToClientAsync(ws, bytes);
        }
    }

    private async Task SendToClientAsync(WebSocket ws, byte[] bytes)
    {
        if (!_sendLocks.TryGetValue(ws, out var sem)) return;
        await sem.WaitAsync();
        try
        {
            if (ws.State == WebSocketState.Open)
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch { }
        finally { sem.Release(); }
    }
}
