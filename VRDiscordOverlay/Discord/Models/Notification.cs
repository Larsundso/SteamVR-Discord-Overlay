using SkiaSharp;

namespace VRDiscordOverlay.Discord.Models;

public class OverlayNotification
{
    public string AuthorName { get; set; } = "";
    public string? AuthorAvatarHash { get; set; }
    public string AuthorId { get; set; } = "";
    public string Content { get; set; } = "";
    public string ChannelName { get; set; } = "";
    public string? GuildName { get; set; }
    public string? GuildIconHash { get; set; }
    public string? GuildId { get; set; }
    public SKBitmap? AuthorAvatar { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public float AnimationProgress { get; set; } = 0f;
    public bool IsLeaving { get; set; }
    public float LeaveProgress { get; set; } = 0f;

    public string AuthorAvatarUrl => string.IsNullOrEmpty(AuthorAvatarHash)
        ? $"https://cdn.discordapp.com/embed/avatars/{(long.TryParse(AuthorId, out var uid) ? (uid >> 22) % 6 : 0)}.png"
        : $"https://cdn.discordapp.com/avatars/{AuthorId}/{AuthorAvatarHash}.png?size=32";
}
