using SkiaSharp;

namespace VRDiscordOverlay.Discord.Models;

public class VoiceUser
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string? GlobalName { get; set; }
    public string? Nick { get; set; }
    public string? AvatarHash { get; set; }
    public bool Bot { get; set; }

    public bool IsSpeaking { get; set; }
    public bool SelfMute { get; set; }
    public bool SelfDeaf { get; set; }
    public bool ServerMute { get; set; }
    public bool ServerDeaf { get; set; }
    public int Volume { get; set; } = 100;

    public SKColor RoleColor { get; set; } = SKColors.White;
    public SKBitmap? AvatarBitmap { get; set; }

    public float AnimationProgress { get; set; } = 0f;
    public bool IsLeaving { get; set; }
    public float LeaveProgress { get; set; } = 0f;
    public DateTime JoinTime { get; set; } = DateTime.UtcNow;

    public string DisplayName => Nick ?? GlobalName ?? Username;
    public bool IsMuted => SelfMute || ServerMute;
    public bool IsDeafened => SelfDeaf || ServerDeaf;

    public string AvatarUrl => string.IsNullOrEmpty(AvatarHash)
        ? $"https://cdn.discordapp.com/embed/avatars/{(long.TryParse(Id, out var uid) ? (uid >> 22) % 6 : 0)}.png"
        : $"https://cdn.discordapp.com/avatars/{Id}/{AvatarHash}.png?size=64";
}
