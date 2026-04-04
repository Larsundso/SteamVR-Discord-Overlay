using Newtonsoft.Json;

namespace VRDiscordOverlay.Config;

public static class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
        "vr-discord-overlay-settings.json");

    private static readonly object _lock = new();

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        var json = File.ReadAllText(SettingsPath);
        return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        lock (_lock)
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
        }
    }
}
