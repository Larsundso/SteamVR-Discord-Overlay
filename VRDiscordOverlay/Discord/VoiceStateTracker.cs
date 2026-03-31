using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SkiaSharp;
using VRDiscordOverlay.Discord.Models;

namespace VRDiscordOverlay.Discord;

public class VoiceStateTracker
{
    private readonly ConcurrentDictionary<string, VoiceUser> _users = new();
    private readonly List<OverlayNotification> _notifications = new();
    private readonly HttpClient _httpClient = new();
    private readonly object _lock = new();
    private string? _currentChannelId;
    private string? _currentGuildId;

    public event Action? OnStateChanged;

    public IReadOnlyList<VoiceUser> GetUsers()
    {
        lock (_lock) { return _users.Values.ToList(); }
    }

    public IReadOnlyList<OverlayNotification> GetNotifications()
    {
        lock (_lock) { return _notifications.ToList(); }
    }

    public string? CurrentChannelId => _currentChannelId;
    public string? CurrentGuildId => _currentGuildId;
    public string? CurrentChannelName { get; private set; }

    public void HandleChannelSelect(RpcChannelData? channel)
    {
        lock (_lock) { _users.Clear(); }

        if (channel == null)
        {
            _currentChannelId = null;
            _currentGuildId = null;
            CurrentChannelName = null;
            OnStateChanged?.Invoke();
            return;
        }

        _currentChannelId = channel.Id;
        _currentGuildId = channel.GuildId;
        CurrentChannelName = channel.Name;

        foreach (var vs in channel.VoiceStates)
        {
            var user = CreateVoiceUser(vs);
            user.AnimationProgress = 1f;
            _users[user.Id] = user;
            _ = LoadAvatarAsync(user);
        }

        OnStateChanged?.Invoke();
    }

    public void HandleVoiceStateCreate(RpcVoiceStateData data)
    {
        var user = CreateVoiceUser(data);
        user.AnimationProgress = 0f;
        user.JoinTime = DateTime.UtcNow;
        _users[user.Id] = user;
        _ = LoadAvatarAsync(user);
        OnStateChanged?.Invoke();
    }

    public void HandleVoiceStateUpdate(RpcVoiceStateData data)
    {
        if (_users.TryGetValue(data.User.Id, out var existing))
        {
            existing.SelfMute = data.VoiceState.SelfMute;
            existing.SelfDeaf = data.VoiceState.SelfDeaf;
            existing.ServerMute = data.VoiceState.Mute;
            existing.ServerDeaf = data.VoiceState.Deaf;
            existing.Volume = data.Volume;
            if (data.Nick != null) existing.Nick = data.Nick;
            OnStateChanged?.Invoke();
        }
    }

    public void HandleVoiceStateDelete(RpcVoiceStateData data)
    {
        if (_users.TryGetValue(data.User.Id, out var user))
        {
            user.IsLeaving = true;
            user.LeaveProgress = 0f;
            OnStateChanged?.Invoke();
        }
    }

    public void RemoveUser(string userId)
    {
        if (_users.TryRemove(userId, out var user))
            user.AvatarBitmap?.Dispose();
        OnStateChanged?.Invoke();
    }

    public void HandleSpeakingStart(string userId)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            user.IsSpeaking = true;
            OnStateChanged?.Invoke();
        }
    }

    public void HandleSpeakingStop(string userId)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            user.IsSpeaking = false;
            OnStateChanged?.Invoke();
        }
    }

    public void HandleNotification(Newtonsoft.Json.Linq.JObject data)
    {
        var msg = data["message"];
        if (msg == null) return;

        var author = msg["author"];
        if (author == null) return;

        var content = msg["content"]?.ToString() ?? "";
        content = ResolveMentions(content, msg);

        var notification = new OverlayNotification
        {
            AuthorId = author["id"]?.ToString() ?? "",
            AuthorName = msg["nick"]?.ToString()
                         ?? author["global_name"]?.ToString()
                         ?? author["username"]?.ToString() ?? "Unknown",
            AuthorAvatarHash = author["avatar"]?.ToString(),
            Content = string.IsNullOrWhiteSpace(content)
                ? "[Open Discord to view]"
                : content,
            ChannelName = data["channel_id"]?.ToString() ?? "",
            CreatedAt = DateTime.UtcNow,
            AnimationProgress = 0f,
        };

        lock (_lock) { _notifications.Add(notification); }
        _ = LoadAvatarForNotification(notification);
        OnStateChanged?.Invoke();
    }

    private async Task LoadAvatarForNotification(OverlayNotification n)
    {
        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(n.AuthorAvatarUrl);
            n.AuthorAvatar = SKBitmap.Decode(bytes);
            OnStateChanged?.Invoke();
        }
        catch { }
    }

    private static string ResolveMentions(string content, Newtonsoft.Json.Linq.JToken msg)
    {
        var mentions = msg["mentions"];
        if (mentions != null)
        {
            foreach (var m in mentions)
            {
                var id = m["id"]?.ToString();
                if (id == null) continue;
                var name = m["nick"]?.ToString()
                           ?? m["global_name"]?.ToString()
                           ?? m["username"]?.ToString() ?? id;
                content = content.Replace($"<@{id}>", $"@{name}");
                content = content.Replace($"<@!{id}>", $"@{name}");
            }
        }

        content = Regex.Replace(content, @"<@&\d+>", "@role");
        content = Regex.Replace(content, @"<#\d+>", "#channel");
        content = Regex.Replace(content, @"<a?:(\w+):\d+>", ":$1:");

        return content;
    }

    private VoiceUser CreateVoiceUser(RpcVoiceStateData data) => new()
    {
        Id = data.User.Id,
        Username = data.User.Username,
        GlobalName = data.User.GlobalName,
        Nick = data.Nick,
        AvatarHash = data.User.Avatar,
        Bot = data.User.Bot,
        SelfMute = data.VoiceState.SelfMute,
        SelfDeaf = data.VoiceState.SelfDeaf,
        ServerMute = data.VoiceState.Mute,
        ServerDeaf = data.VoiceState.Deaf,
        Volume = data.Volume,
    };

    private async Task LoadAvatarAsync(VoiceUser user)
    {
        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(user.AvatarUrl);
            user.AvatarBitmap = SKBitmap.Decode(bytes);
            OnStateChanged?.Invoke();
        }
        catch { }
    }

    public void UpdateAnimations(float deltaTime)
    {
        bool changed = false;
        var toRemove = new List<string>();

        foreach (var user in _users.Values)
        {
            if (user.IsLeaving)
            {
                user.LeaveProgress += deltaTime * 3f;
                if (user.LeaveProgress >= 1f) toRemove.Add(user.Id);
                changed = true;
            }
            else if (user.AnimationProgress < 1f)
            {
                user.AnimationProgress = Math.Min(1f, user.AnimationProgress + deltaTime * 3f);
                changed = true;
            }
        }

        foreach (var id in toRemove) RemoveUser(id);

        lock (_lock)
        {
            for (int i = _notifications.Count - 1; i >= 0; i--)
            {
                var n = _notifications[i];
                if (n.IsLeaving)
                {
                    n.LeaveProgress += deltaTime * 3f;
                    if (n.LeaveProgress >= 1f)
                    {
                        n.AuthorAvatar?.Dispose();
                        _notifications.RemoveAt(i);
                    }
                    changed = true;
                }
                else if (n.AnimationProgress < 1f)
                {
                    n.AnimationProgress = Math.Min(1f, n.AnimationProgress + deltaTime * 3f);
                    changed = true;
                }
                else if ((DateTime.UtcNow - n.CreatedAt).TotalSeconds >= 5 && !n.IsLeaving)
                {
                    n.IsLeaving = true;
                    changed = true;
                }
            }
        }

        if (changed) OnStateChanged?.Invoke();
    }
}
