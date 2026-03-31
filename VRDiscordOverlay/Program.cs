using VRDiscordOverlay.Config;
using VRDiscordOverlay.Discord;
using VRDiscordOverlay.Rendering;
using VRDiscordOverlay.VR;

namespace VRDiscordOverlay;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "Discord VC Overlay";
        ConsoleUI.Init("  ? help   arrows move   +/- depth   S save   Ctrl+C quit");

        ConsoleUI.Log("Discord VC Overlay for SteamVR");
        ConsoleUI.Log("");

        var settings = SettingsManager.Load();


        var overlay = new SteamVrOverlay(settings);
        if (!overlay.Initialize())
        {
            ConsoleUI.Log("Could not connect to SteamVR. Is it running?");
            Console.ReadKey();
            return;
        }

        var renderer = new OverlayRenderer(settings);
        var voiceTracker = new VoiceStateTracker();
        var discord = new DiscordRpcClient(AppSettings.DiscordClientId, AppSettings.DiscordClientSecret);


        discord.OnVoiceStateCreate += voiceTracker.HandleVoiceStateCreate;
        discord.OnVoiceStateUpdate += voiceTracker.HandleVoiceStateUpdate;
        discord.OnVoiceStateDelete += voiceTracker.HandleVoiceStateDelete;
        discord.OnSpeakingStart += voiceTracker.HandleSpeakingStart;
        discord.OnSpeakingStop += voiceTracker.HandleSpeakingStop;
        discord.OnNotificationCreate += voiceTracker.HandleNotification;
        discord.OnVoiceChannelSelect += async (channel) =>
        {
            voiceTracker.HandleChannelSelect(channel);
            if (channel != null)
            {
                ConsoleUI.Log($"Joined #{channel.Name} ({channel.VoiceStates.Count} users)");
                await discord.SubscribeToVoiceChannel(channel.Id);
            }
            else
            {
                ConsoleUI.Log("Left voice channel");
            }
        };

        discord.OnReady += () =>
        {
            settings.AccessToken = discord.AccessToken;
            SettingsManager.Save(settings);
        };

        discord.OnError += (msg) => ConsoleUI.Log($"Error: {msg}");
        discord.OnDisconnected += () => ConsoleUI.Log("Disconnected from Discord");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            ConsoleUI.Log("Connecting to Discord...");
            await discord.ConnectAsync(settings.AccessToken, settings.DiscordPipe, cts.Token);
        }
        catch (Exception ex)
        {
            ConsoleUI.Log($"Could not connect: {ex.Message}");
            ConsoleUI.Log("Make sure Discord is running.");
            Console.ReadKey();
            overlay.Dispose();
            return;
        }


        var lastFrame = DateTime.UtcNow;
        int redrawFlag = 1;

        voiceTracker.OnStateChanged += () => Interlocked.Exchange(ref redrawFlag, 1);

        _ = Task.Run(() => HandleKeyboardInput(settings, overlay, discord, ref redrawFlag, cts.Token), cts.Token);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                float deltaTime = (float)(now - lastFrame).TotalSeconds;
                lastFrame = now;

                voiceTracker.UpdateAnimations(deltaTime);

                if (Interlocked.Exchange(ref redrawFlag, 0) == 1)
                {
                    var users = voiceTracker.GetUsers();
                    var notifs = voiceTracker.GetNotifications();
                    var (pixels, width, height) = renderer.Render(
                        users, notifs, voiceTracker.CurrentChannelName,
                        settings.ShowOnlyUnmuted, settings.MutedUserThreshold);
                    overlay.SetTexture(pixels, width, height);
                }

                bool hasAnimations = voiceTracker.GetUsers().Any(u =>
                    u.AnimationProgress < 1f || u.IsLeaving);
                await Task.Delay(hasAnimations ? 16 : 100, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }

        discord.Dispose();
        overlay.Dispose();
    }

    private static void HandleKeyboardInput(AppSettings settings, SteamVrOverlay overlay, DiscordRpcClient discord, ref int redrawFlag, CancellationToken ct)
    {
        const float moveStep = 0.02f;
        const float depthStep = 0.05f;
        const float angleStep = 2f; // degrees
        bool helpShown = false;

        while (!ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable) { Thread.Sleep(50); continue; }

            var key = Console.ReadKey(intercept: true);
            bool moved = false;

            if (key.KeyChar == '?')
            {
                helpShown = !helpShown;
                if (helpShown)
                {
                    ConsoleUI.Log("");
                    ConsoleUI.Log("  Controls:");
                    ConsoleUI.Log("    Arrow keys   Move overlay left/right/up/down");
                    ConsoleUI.Log("    + / -        Push further / pull closer");
                    ConsoleUI.Log("    Q / E        Rotate left / right (yaw)");
                    ConsoleUI.Log("    R / F        Tilt up / down (pitch)");
                    ConsoleUI.Log("    M            Toggle show only unmuted");
                    ConsoleUI.Log("    T / Shift+T  Muted user threshold +/- 1");
                    ConsoleUI.Log("    S            Save settings to file");
                    ConsoleUI.Log("    P            Cycle Discord pipe (0-9 or auto)");
                    ConsoleUI.Log("    A            Re-authorize Discord");
                    ConsoleUI.Log("    Ctrl+C       Exit");
                    ConsoleUI.Log("");
                    PrintPosition(settings);
                }
            }
            else switch (key.Key)
            {
                case ConsoleKey.LeftArrow:  settings.OverlayX -= moveStep; moved = true; break;
                case ConsoleKey.RightArrow: settings.OverlayX += moveStep; moved = true; break;
                case ConsoleKey.UpArrow:    settings.OverlayY += moveStep; moved = true; break;
                case ConsoleKey.DownArrow:  settings.OverlayY -= moveStep; moved = true; break;
                case ConsoleKey.OemPlus:
                case ConsoleKey.Add:        settings.OverlayZ += depthStep; moved = true; break;
                case ConsoleKey.OemMinus:
                case ConsoleKey.Subtract:   settings.OverlayZ -= depthStep; moved = true; break;
                case ConsoleKey.Q:          settings.OverlayYaw -= angleStep; moved = true; break;
                case ConsoleKey.E:          settings.OverlayYaw += angleStep; moved = true; break;
                case ConsoleKey.R:          settings.OverlayPitch -= angleStep; moved = true; break;
                case ConsoleKey.F:          settings.OverlayPitch += angleStep; moved = true; break;
                case ConsoleKey.M:
                    settings.ShowOnlyUnmuted = !settings.ShowOnlyUnmuted;
                    ConsoleUI.Log(settings.ShowOnlyUnmuted
                        ? "Showing only unmuted users"
                        : "Showing all users");
                    Interlocked.Exchange(ref redrawFlag, 1);
                    break;
                case ConsoleKey.T:
                    settings.MutedUserThreshold = key.Modifiers.HasFlag(ConsoleModifiers.Shift)
                        ? Math.Max(1, settings.MutedUserThreshold - 1)
                        : settings.MutedUserThreshold + 1;
                    ConsoleUI.Log($"Muted user threshold: {settings.MutedUserThreshold} (collapse when >= {settings.MutedUserThreshold} muted)");
                    Interlocked.Exchange(ref redrawFlag, 1);
                    break;
                case ConsoleKey.S:
                    SettingsManager.Save(settings);
                    ConsoleUI.Log("Settings saved!");
                    break;
                case ConsoleKey.P:
                    settings.DiscordPipe = settings.DiscordPipe >= 9 ? -1 : settings.DiscordPipe + 1;
                    var pipeLabel = settings.DiscordPipe == -1 ? "auto" : settings.DiscordPipe.ToString();
                    ConsoleUI.Log($"Discord pipe set to {pipeLabel} (restart to apply, or press S then relaunch)");
                    break;
                case ConsoleKey.A:
                    ConsoleUI.Log("Re-authorizing... check Discord for the approval popup.");
                    settings.AccessToken = null;
                    SettingsManager.Save(settings);
                    _ = discord.ForceReauthAsync();
                    break;
            }

            if (moved)
            {
                overlay.UpdatePosition();
                PrintPosition(settings);
                Interlocked.Exchange(ref redrawFlag, 1);
            }
        }
    }

    private static void PrintPosition(AppSettings settings)
    {
        ConsoleUI.SetStatus($"  X={settings.OverlayX:F2} Y={settings.OverlayY:F2} Z={settings.OverlayZ:F2} yaw={settings.OverlayYaw:F0} pitch={settings.OverlayPitch:F0} | ? S Ctrl+C");
    }
}
