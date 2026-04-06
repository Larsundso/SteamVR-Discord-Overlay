namespace VRDiscordOverlay.Config;

public class ButtonSettings
{
    public bool Enabled { get; set; } = true;
    public string AttachTo { get; set; } = "left"; // "left", "right", "hmd", "playspace"
    public float X { get; set; } = 0f;
    public float Y { get; set; } = 0f;
    public float Z { get; set; } = -0.05f;
    public float Yaw { get; set; } = 0f;
    public float Pitch { get; set; } = 0f;
    public float Rotation { get; set; } = 0f;
    public float Scale { get; set; } = 0.04f;
    public float Opacity { get; set; } = 0.9f;
}
