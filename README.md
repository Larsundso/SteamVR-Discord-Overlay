# VR Discord VC Overlay

A SteamVR overlay that shows your Discord voice channel members in VR. See who's talking, muted, or deafened without taking off your headset. See Discord notifications and subscribe to text channels for in-VR message viewing. Control the overlay with keyboard shortcuts or a web dashboard.

https://github.com/Larsundso/SteamVR-Discord-Overlay/raw/main/showcase.mp4

## Features

- **Voice channel display** - Shows all users in your current VC with their avatar, nickname, and role color
- **Speaking indicator** - Green glow when someone is talking
- **Mute/deaf icons** - Distinguishes self-mute (gray) from server mute (red)
- **Muted user collapsing** - Configurable threshold to collapse muted users into "... and N muted / N deafened"
- **Notifications** - Shows pings/DMs as a card below the VC list with author avatar, name, and message content (auto-dismisses after 5s)
- **Channel message subscriptions** - Subscribe to up to 5 text channels to see messages in both the VR overlay and the web dashboard. Supports message edits and deletes. Subscriptions persist across restarts.
- **Mention resolution** - `@user`, `@role`, `#channel`, and `:emoji:` rendered as readable text in notifications
- **Join/leave animations** - Users slide in from the left and phase out when leaving
- **Channel header** - Shows channel name and voice connection status
- **Web dashboard** - Browser-based control panel with live console, overlay controls, and channel subscription browser
- **Adjustable position** - Move, rotate, and angle the overlay from the console or web dashboard
- **Auto-start** - Registers with SteamVR to launch automatically
- **Portable** - Single .exe, no install needed

## How it works

Connects to Discord's local IPC pipe (`discord-ipc-0` through `discord-ipc-9`) using the RPC protocol. No bot token needed - it talks directly to your running Discord client. The overlay is rendered with SkiaSharp and displayed via a DirectX 11 texture through OpenVR.

## Getting started

1. Download `VRDiscordOverlay.exe` and `vrmanifest.json` (keep them in the same folder)
2. Start SteamVR
3. Start Discord
4. Run `VRDiscordOverlay.exe`
5. Discord will show an authorization popup - click **Authorize**
6. Join a voice channel

That's it. The overlay appears in VR showing your VC members. A web dashboard opens automatically at `http://localhost:39039`.

## Controls

### Keyboard

Press `?` in the console to see all controls.

| Key | Action |
|-----|--------|
| Arrow keys | Move overlay left/right/up/down |
| `+` / `-` | Push further / pull closer |
| `Q` / `E` | Rotate left / right (yaw) |
| `R` / `F` | Tilt up / down (pitch) |
| `M` | Toggle show only unmuted users |
| `T` / `Shift+T` | Increase / decrease muted user threshold |
| `S` | Save settings to file |
| `P` | Cycle Discord pipe (auto, 0-9) |
| `A` | Re-authorize Discord (new permission popup) |
| `?` | Show controls |
| `Ctrl+C` | Exit |

### Web Dashboard

Opens automatically at `http://localhost:39039`. Provides:

- **Overlay controls** - Position, rotation, scale, opacity, display settings
- **Channel browser** - Browse servers and text channels, subscribe with checkboxes
- **Active subscriptions** - Quick-unsub list above the search filters
- **Live console** - Color-coded log output with message display from subscribed channels
- **Message tracking** - Edits show `(edited)`, deletes show strikethrough
- **Discord controls** - Pipe selection, re-authorize button

## Settings

Settings are saved to `vr-discord-overlay-settings.json` next to the exe. Editable fields:

| Setting | Default | Description |
|---------|---------|-------------|
| `OverlayX` | -0.5 | Horizontal position (meters from HMD) |
| `OverlayY` | 0.1 | Vertical position |
| `OverlayZ` | -1.0 | Depth (negative = in front) |
| `OverlayWidth` | 0.4 | Scale factor (meters per 200px) |
| `OverlayYaw` | 0 | Horizontal angle (degrees) |
| `OverlayPitch` | 0 | Vertical angle (degrees) |
| `ShowOnlyUnmuted` | false | Hide all muted users |
| `MutedUserThreshold` | 5 | Collapse muted users when count >= this |
| `AutoStartWithSteamVR` | true | Launch with SteamVR |
| `OverlayOpacity` | 1.0 | Overlay transparency |
| `DiscordPipe` | -1 | IPC pipe (-1 = auto, 0-9 = specific) |
| `SavedSubscriptions` | {} | Persisted channel subscriptions (id: name) |

## Building from source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
cd VRDiscordOverlay
dotnet publish -c Release
```

Output: `bin/Release/net8.0/win-x64/publish/VRDiscordOverlay.exe` (~76 MB, self-contained, no .NET runtime needed)

## Architecture

```
VRDiscordOverlay/
  Program.cs              Main loop, event wiring, keyboard controls
  ConsoleUI.cs            Pinned status bar console
  Config/
    AppSettings.cs        Settings model + embedded Discord credentials
    SettingsManager.cs    JSON persistence
  Discord/
    DiscordRpcClient.cs   IPC pipe connection, RPC protocol, auth flow, message subscriptions
    VoiceStateTracker.cs  User list, notifications, message events, animations
    Models/
      DiscordUser.cs      VoiceUser with display name, avatar, states
      RpcMessages.cs      RPC JSON models
      Notification.cs     Notification model
  Rendering/
    OverlayRenderer.cs    SkiaSharp 2D rendering (cards, icons, text)
  VR/
    SteamVrOverlay.cs     OpenVR overlay + D3D11 texture upload
  Web/
    WebServer.cs          Embedded Kestrel server, REST API, WebSocket broadcasts
    DashboardHtml.cs      Full dashboard UI (HTML/CSS/JS) as embedded string
```

## Tech stack

- **C# / .NET 8** - Single-file self-contained publish
- **Discord RPC** - Local IPC pipe, no bot token
- **SkiaSharp** - 2D rendering (BGRA bitmaps)
- **Vortice.Direct3D11** - Flicker-free texture upload to SteamVR
- **OpenVR (OVRSharp)** - SteamVR overlay management
- **ASP.NET Core (Kestrel)** - Embedded web server for dashboard

## Limitations

- **Role colors** - Discord RPC doesn't provide role color data. Names display in white.
- **Video/streaming status** - RPC only reports your own camera/stream state, not other users'.
- **Voice channel status** - The custom text on voice channels isn't available through RPC.
- **Discord RPC private beta** - Unapproved apps work for up to 50 testers. For wider distribution, Discord approval is needed.
