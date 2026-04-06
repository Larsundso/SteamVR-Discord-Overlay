namespace VRDiscordOverlay.Config;

public class AppSettings
{
    public const string DiscordClientId = "1488318404933849089";
    public const string DiscordClientSecret = "FpTIPZXOT-9XxD8xqAhdngJC1cuAWalY";

    public string? AccessToken { get; set; }

    public float OverlayX { get; set; } = -0.5f;
    public float OverlayY { get; set; } = 0.1f;
    public float OverlayZ { get; set; } = -1.0f;
    public float OverlayWidth { get; set; } = 0.4f;
    public float OverlayPitch { get; set; } = 0f;
    public float OverlayYaw { get; set; } = 0f;

    public bool ShowOnlyUnmuted { get; set; } = false;
    public int MutedUserThreshold { get; set; } = 5;

    public bool AutoStartWithSteamVR { get; set; } = true;
    public int DiscordPipe { get; set; } = -1; // -1 = auto, 0-9 = specific pipe

    public Dictionary<string, string> SavedSubscriptions { get; set; } = new();

    public int CardHeight { get; set; } = 52;
    public int CardPadding { get; set; } = 4;
    public float OverlayOpacity { get; set; } = 1.0f;

    public ButtonSettings MuteButton { get; set; } = new();
    public ButtonSettings DeafenButton { get; set; } = new() { X = 0.05f };
}
