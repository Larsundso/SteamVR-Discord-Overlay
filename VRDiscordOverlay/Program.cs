using System.Diagnostics;
using Valve.VR;
using VRDiscordOverlay.Config;
using VRDiscordOverlay.Discord;
using VRDiscordOverlay.Rendering;
using VRDiscordOverlay.VR;
using VRDiscordOverlay.Web;

namespace VRDiscordOverlay;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "Discord VC Overlay";

        try { await Run(args); }
        catch (Exception ex)
        {
            var logPath = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath) ?? ".",
                "crash.log");
            File.WriteAllText(logPath, $"{DateTime.Now}\n{ex}");
        }
    }

    static async Task Run(string[] args)
    {
        var settings = SettingsManager.Load();
        var webServer = new WebServer(settings);
        var cts = new CancellationTokenSource();

        await webServer.StartAsync(cts.Token);
        ConsoleUI.Init($"  Dashboard: http://localhost:{webServer.Port}  |  ? help  Ctrl+C quit", webServer);

        ConsoleUI.Log("Discord VC Overlay for SteamVR");
        ConsoleUI.Log($"Dashboard: http://localhost:{webServer.Port}");
        ConsoleUI.Log("");

        Process? browserProcess = null;
        try { browserProcess = Process.Start(new ProcessStartInfo($"http://localhost:{webServer.Port}") { UseShellExecute = true }); }
        catch { }

        var overlay = new SteamVrOverlay(settings);
        if (!overlay.Initialize())
        {
            ConsoleUI.Log("Could not connect to SteamVR. Is it running?");
            Console.ReadKey();
            return;
        }

        int redrawFlag = 1;
        int buttonRedrawFlag = 0;

        var renderer = new OverlayRenderer(settings);
        if (string.IsNullOrEmpty(settings.DiscordClientId) || string.IsNullOrEmpty(settings.DiscordClientSecret))
        {
            ConsoleUI.Log("");
            ConsoleUI.Log("Discord app not configured. Open the dashboard to set up:");
            ConsoleUI.Log($"  http://localhost:{webServer.Port}");
            ConsoleUI.Log("");

            while (string.IsNullOrEmpty(settings.DiscordClientId) || string.IsNullOrEmpty(settings.DiscordClientSecret))
            {
                await Task.Delay(1000, cts.Token);
                settings = SettingsManager.Load();
            }
            ConsoleUI.Log("Discord app configured! Continuing...");
        }

        var voiceTracker = new VoiceStateTracker();
        var discord = new DiscordRpcClient(settings.DiscordClientId, settings.DiscordClientSecret);

        var muteBtn = new VrButton("vr.discord.mute", "Mute", overlay.D3dDevice!, overlay.D3dContext!);
        var deafenBtn = new VrButton("vr.discord.deafen", "Deafen", overlay.D3dDevice!, overlay.D3dContext!);
        muteBtn.Initialize();
        deafenBtn.Initialize();
        bool isMuted = false, isDeafened = false;

        void RenderButtons()
        {
            var (mp, mw, mh) = renderer.RenderButton("mute", isMuted);
            muteBtn.SetTexture(mp, mw, mh);
            var (dp, dw, dh) = renderer.RenderButton("deafen", isDeafened);
            deafenBtn.SetTexture(dp, dw, dh);
        }

        void UpdateButtonPositions()
        {
            muteBtn.UpdatePosition(settings.MuteButton);
            deafenBtn.UpdatePosition(settings.DeafenButton);
        }

        muteBtn.OnClicked += () => _ = discord.SetVoiceSettingsAsync(mute: !isMuted);
        deafenBtn.OnClicked += () => _ = discord.SetVoiceSettingsAsync(deaf: !isDeafened);

        UpdateButtonPositions();
        RenderButtons();

        discord.OnVoiceStateCreate += voiceTracker.HandleVoiceStateCreate;
        discord.OnVoiceStateUpdate += voiceTracker.HandleVoiceStateUpdate;
        discord.OnVoiceStateDelete += voiceTracker.HandleVoiceStateDelete;
        discord.OnSpeakingStart += voiceTracker.HandleSpeakingStart;
        discord.OnSpeakingStop += voiceTracker.HandleSpeakingStop;
        discord.OnNotificationCreate += voiceTracker.HandleNotification;
        discord.OnVoiceConnectionStatus += (state) =>
        {
            voiceTracker.HandleVoiceConnectionStatus(state);
            ConsoleUI.BroadcastState(new
            {
                voiceState = state,
                channel = voiceTracker.CurrentChannelName,
                pipe = discord.ConnectedPipe
            });
        };
        discord.OnVoiceSettingsUpdate += (m, d) =>
        {
            isMuted = m;
            isDeafened = d;
            Interlocked.Exchange(ref buttonRedrawFlag, 1);
        };
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

        discord.OnAccessTokenInvalidated += () =>
        {
            settings.AccessToken = null;
            SettingsManager.Save(settings);
        };

        discord.OnReady += () =>
        {
            settings.AccessToken = discord.AccessToken;
            SettingsManager.Save(settings);
            ConsoleUI.BroadcastState(new
            {
                voiceState = "CONNECTED",
                pipe = discord.ConnectedPipe
            });

            if (settings.SavedSubscriptions.Count > 0)
            {
                _ = Task.Run(async () =>
                {
                    int count = 0;
                    foreach (var (chId, saved) in settings.SavedSubscriptions.ToList())
                    {
                        var parts = saved.Split('|', 2);
                        var chName = parts[0];
                        var guildName = parts.Length > 1 ? parts[1] : "";
                        try
                        {
                            await discord.SubscribeToTextChannel(chId);
                            voiceTracker.RegisterChannelInfo(chId, chName, guildName);
                            ConsoleUI.Log($"Restored subscription: #{chName}" + (guildName != "" ? $" ({guildName})" : ""));
                            count++;
                        }
                        catch { ConsoleUI.Log($"Failed to restore: #{chName}"); }
                    }
                    ConsoleUI.Log($"Restored {count} channel subscription(s)");
                });
            }

            _ = Task.Run(async () =>
            {
                var (m, d) = await discord.GetVoiceSettingsAsync();
                isMuted = m;
                isDeafened = d;
                Interlocked.Exchange(ref buttonRedrawFlag, 1);
            });
        };

        discord.OnMessageCreate += (data) =>
        {
            webServer.BroadcastMessage("message_create", data);
            voiceTracker.HandleMessageCreate(data);
        };
        discord.OnMessageUpdate += (data) => webServer.BroadcastMessage("message_update", data);
        discord.OnMessageDelete += (data) => webServer.BroadcastMessage("message_delete", data);

        discord.OnError += (msg) => ConsoleUI.Log($"Error: {msg}");
        discord.OnDisconnected += () => ConsoleUI.Log("Disconnected from Discord");

        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        webServer.RegisterChannelInfo = (id, ch, guild) => voiceTracker.RegisterChannelInfo(id, ch, guild);
        webServer.GetGuilds = () => discord.GetGuildsAsync();
        webServer.GetChannels = (id) => discord.GetChannelsAsync(id);
        webServer.SubscribeChannel = (id) => discord.SubscribeToTextChannel(id);
        webServer.UnsubscribeChannel = (id) => discord.UnsubscribeFromTextChannel(id);
        webServer.GetSubscribedChannels = () => discord.GetSubscribedTextChannels();

        webServer.OnCommand += (name, arg) =>
        {
            switch (name)
            {
                case "reauth":
                    ConsoleUI.Log("Re-authorizing... check Discord for the approval popup.");
                    settings.AccessToken = null;
                    SettingsManager.Save(settings);
                    _ = discord.ForceReauthAsync();
                    break;
                case "settings_changed":
                    SettingsManager.Save(settings);
                    Interlocked.Exchange(ref redrawFlag, 1);
                    Interlocked.Exchange(ref buttonRedrawFlag, 1);
                    break;
            }
        };

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
                bool dashboardVisible = OpenVR.Overlay.IsDashboardVisible();
                if (dashboardVisible && settings.MuteButton.Enabled) muteBtn.Show();
                else muteBtn.Hide();
                if (dashboardVisible && settings.DeafenButton.Enabled) deafenBtn.Show();
                else deafenBtn.Hide();

                muteBtn.PollClick();
                deafenBtn.PollClick();

                if (Interlocked.Exchange(ref buttonRedrawFlag, 0) == 1)
                {
                    overlay.UpdatePosition();
                    UpdateButtonPositions();
                    RenderButtons();
                }

                if (Interlocked.Exchange(ref redrawFlag, 0) == 1)
                {
                    var users = voiceTracker.GetUsers();
                    var notifs = voiceTracker.GetNotifications();
                    var (pixels, width, height) = renderer.Render(
                        users, notifs, voiceTracker.CurrentChannelName,
                        voiceTracker.VoiceConnectionState,
                        settings.ShowOnlyUnmuted, settings.MutedUserThreshold);
                    overlay.SetTexture(pixels, width, height);
                }

                bool hasAnimations = voiceTracker.GetUsers().Any(u =>
                    u.AnimationProgress < 1f || u.IsLeaving);
                await Task.Delay(hasAnimations ? 16 : 100, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }

        try { if (browserProcess is { HasExited: false }) browserProcess.Kill(); } catch { }
        discord.Dispose();
        muteBtn.Dispose();
        deafenBtn.Dispose();
        overlay.Dispose();
    }

    private static void HandleKeyboardInput(AppSettings settings, SteamVrOverlay overlay, DiscordRpcClient discord, ref int redrawFlag, CancellationToken ct)
    {
        const float moveStep = 0.02f;
        const float depthStep = 0.05f;
        const float angleStep = 2f;
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
                    ConsoleUI.Log(settings.ShowOnlyUnmuted ? "Showing only unmuted users" : "Showing all users");
                    Interlocked.Exchange(ref redrawFlag, 1);
                    break;
                case ConsoleKey.T:
                    settings.MutedUserThreshold = key.Modifiers.HasFlag(ConsoleModifiers.Shift)
                        ? Math.Max(1, settings.MutedUserThreshold - 1)
                        : settings.MutedUserThreshold + 1;
                    ConsoleUI.Log($"Muted user threshold: {settings.MutedUserThreshold}");
                    Interlocked.Exchange(ref redrawFlag, 1);
                    break;
                case ConsoleKey.S:
                    SettingsManager.Save(settings);
                    ConsoleUI.Log("Settings saved!");
                    break;
                case ConsoleKey.P:
                    settings.DiscordPipe = settings.DiscordPipe >= 9 ? -1 : settings.DiscordPipe + 1;
                    var pipeLabel = settings.DiscordPipe == -1 ? "auto" : settings.DiscordPipe.ToString();
                    ConsoleUI.Log($"Discord pipe set to {pipeLabel} (save & restart to apply)");
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
