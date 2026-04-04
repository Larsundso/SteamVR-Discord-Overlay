using SkiaSharp;
using VRDiscordOverlay.Config;
using VRDiscordOverlay.Discord.Models;

namespace VRDiscordOverlay.Rendering;

public class OverlayRenderer
{
    private readonly AppSettings _settings;

    private const int AvatarSize = 36;
    private const int AvatarMargin = 8;
    private const int TextLeftMargin = 52;
    private const int IconSize = 16;
    private const int IconRightMargin = 8;
    private const int CardRightPad = 12;
    private const int CornerRadius = 8;
    private const int MinWidth = 120;

    private const int HeaderHeight = 28;

    private static readonly SKColor SpeakingGreen = new(67, 181, 129, 80);
    private static readonly SKColor SpeakingBorder = new(67, 181, 129, 200);
    private static readonly SKColor MutedOverlay = new(0, 0, 0, 100);
    private static readonly SKColor CounterTextColor = new(180, 180, 180, 220);
    private static readonly SKColor HeaderColor = new(150, 150, 150, 220);
    private static readonly SKColor HeaderCountColor = new(100, 100, 100, 180);

    private readonly SKFont _nameFont;
    private readonly SKFont _counterFont;
    private readonly SKFont _headerFont;
    private readonly SKPaint _namePaint;
    private readonly SKPaint _counterPaint;
    private readonly SKPaint _headerPaint;
    private readonly SKPaint _headerCountPaint;

    public OverlayRenderer(AppSettings settings)
    {
        _settings = settings;

        var typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                       ?? SKTypeface.Default;

        _nameFont = new SKFont(typeface, 15);
        _counterFont = new SKFont(typeface, 12);

        _namePaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White,
            TextSize = 15,
            Typeface = typeface,
        };

        _counterPaint = new SKPaint
        {
            IsAntialias = true,
            Color = CounterTextColor,
            TextSize = 12,
            Typeface = typeface,
        };

        _headerFont = new SKFont(typeface, 12);
        _headerPaint = new SKPaint
        {
            IsAntialias = true,
            Color = HeaderColor,
            TextSize = 12,
            Typeface = typeface,
        };
        _headerCountPaint = new SKPaint
        {
            IsAntialias = true,
            Color = HeaderCountColor,
            TextSize = 11,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default,
        };
    }

    public (byte[] pixels, int width, int height) Render(IReadOnlyList<VoiceUser> allUsers, IReadOnlyList<OverlayNotification> notifications, string? channelName, string voiceConnectionState, bool showOnlyUnmuted, int mutedThreshold)
    {
        var activeUsers = new List<VoiceUser>();
        var mutedUsers = new List<VoiceUser>();
        var leavingUsers = new List<VoiceUser>();

        foreach (var user in allUsers)
        {
            if (user.IsLeaving)
                leavingUsers.Add(user);
            else if (showOnlyUnmuted && user.IsMuted)
                continue;
            else if (user.IsMuted || user.IsDeafened)
                mutedUsers.Add(user);
            else
                activeUsers.Add(user);
        }

        int mutedCount = mutedUsers.Count;
        int deafenedCount = mutedUsers.Count(u => u.IsDeafened);
        int mutedOnlyCount = mutedCount - deafenedCount;
        bool hideMuted = mutedCount >= mutedThreshold;

        int cardH = _settings.CardHeight;
        int pad = _settings.CardPadding;

        float maxNameWidth = 0;
        foreach (var user in allUsers)
        {
            float w = _namePaint.MeasureText(user.DisplayName);
            if (w > maxNameWidth) maxNameWidth = w;
        }
        int cardWidth = TextLeftMargin + (int)maxNameWidth + IconRightMargin + IconSize + CardRightPad;

        var joinLeaveNotifs = notifications.Where(n => n.IsJoinLeave).ToList();
        var messageNotifs = notifications.Where(n => !n.IsJoinLeave).ToList();

        float maxJoinLeaveWidth = 0;
        foreach (var n in joinLeaveNotifs)
        {
            float w = _counterPaint.MeasureText(n.AuthorName) + 4 + _counterPaint.MeasureText(n.Content);
            if (w > maxJoinLeaveWidth) maxJoinLeaveWidth = w;
        }
        foreach (var user in allUsers)
        {
            float w = _counterPaint.MeasureText(user.DisplayName) + 4 + _counterPaint.MeasureText("left the channel");
            if (w > maxJoinLeaveWidth) maxJoinLeaveWidth = w;
        }
        int joinLeaveWidth = AvatarMargin + (int)Math.Ceiling(maxJoinLeaveWidth) + 24;

        string headerText = channelName != null ? $"# {channelName}" : "";
        string headerCount = $" — {allUsers.Count} users";
        float headerTextWidth = _headerPaint.MeasureText(headerText) + _headerCountPaint.MeasureText(headerCount);
        int width = Math.Max(MinWidth, Math.Max(cardWidth, Math.Max(joinLeaveWidth, AvatarMargin + (int)headerTextWidth + CardRightPad)));

        bool hasHeader = channelName != null;
        int totalCards = activeUsers.Count + leavingUsers.Count;
        if (!hideMuted) totalCards += mutedUsers.Count;
        int counterH = 0;
        if (hideMuted)
        {
            if (mutedOnlyCount > 0) counterH += 20;
            if (deafenedCount > 0) counterH += 20;
        }
        int headerH = hasHeader ? HeaderHeight : 0;
        int notifCardH = 56;
        int joinLeaveH = joinLeaveNotifs.Count > 0 ? joinLeaveNotifs.Count * (cardH + pad) + 8 : 0;
        int msgNotifH = messageNotifs.Count > 0 ? messageNotifs.Count * (notifCardH + pad) + 8 : 0;
        int height = Math.Max(cardH + pad, headerH + totalCards * (cardH + pad) + counterH + joinLeaveH + msgNotifH + pad);

        foreach (var n in messageNotifs)
        {
            float nw = TextLeftMargin + Math.Max(
                _namePaint.MeasureText(n.AuthorName),
                _counterPaint.MeasureText(n.Content.Length > 50 ? n.Content[..50] : n.Content)
            ) + CardRightPad;
            if ((int)nw > width) width = (int)nw;
        }

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        int y = pad;

        if (hasHeader)
        {
            float hx = AvatarMargin;

            var dotColor = voiceConnectionState switch
            {
                "VOICE_CONNECTED" or "CONNECTED" => new SKColor(67, 181, 129),
                "VOICE_CONNECTING" or "CONNECTING" or "AUTHENTICATING" or "AWAITING_ENDPOINT" => new SKColor(250, 168, 26),
                "NO_ROUTE" => new SKColor(237, 66, 69),
                _ => new SKColor(116, 127, 141),
            };
            using var dotPaint = new SKPaint { IsAntialias = true, Color = dotColor, Style = SKPaintStyle.Fill };
            canvas.DrawCircle(hx + 4, y + 13, 4, dotPaint);
            hx += 14;

            canvas.DrawText(headerText, hx, y + 16, _headerFont, _headerPaint);
            hx += _headerPaint.MeasureText(headerText);
            canvas.DrawText(headerCount, hx, y + 16, _counterFont, _headerCountPaint);
            y += HeaderHeight;
        }

        foreach (var user in activeUsers)
        {
            float slideX = EaseOutCubic(user.AnimationProgress);
            DrawUserCard(canvas, user, (int)((slideX - 1f) * width), y, width, cardH, false);
            y += cardH + pad;
        }

        if (!hideMuted)
        {
            foreach (var user in mutedUsers)
            {
                float slideX = EaseOutCubic(user.AnimationProgress);
                DrawUserCard(canvas, user, (int)((slideX - 1f) * width), y, width, cardH, true);
                y += cardH + pad;
            }
        }
        else
        {
            if (mutedOnlyCount > 0)
            {
                DrawCounterText(canvas, $"... and {mutedOnlyCount} muted", y, width);
                y += 20;
            }
            if (deafenedCount > 0)
            {
                DrawCounterText(canvas, $"... and {deafenedCount} deafened", y, width);
                y += 20;
            }
        }

        foreach (var user in leavingUsers)
        {
            float slideX = 1f - EaseInCubic(user.LeaveProgress);
            DrawUserCard(canvas, user, (int)((slideX - 1f) * width), y, width, cardH, false);
            y += cardH + pad;
        }

        if (joinLeaveNotifs.Count > 0)
        {
            y += 4;
            using var sepPaint = new SKPaint { Color = new SKColor(80, 80, 80, 100), StrokeWidth = 1 };
            canvas.DrawLine(8, y, width - 8, y, sepPaint);
            y += 4;

            foreach (var n in joinLeaveNotifs)
            {
                float slideX = n.IsLeaving
                    ? 1f - EaseInCubic(n.LeaveProgress)
                    : EaseOutCubic(n.AnimationProgress);
                DrawJoinLeaveNotification(canvas, n, (int)((slideX - 1f) * width), y, width, cardH);
                y += cardH + pad;
            }
        }

        if (messageNotifs.Count > 0)
        {
            if (joinLeaveNotifs.Count == 0)
            {
                y += 4;
                using var sepPaint = new SKPaint { Color = new SKColor(80, 80, 80, 100), StrokeWidth = 1 };
                canvas.DrawLine(8, y, width - 8, y, sepPaint);
                y += 4;
            }

            foreach (var n in messageNotifs)
            {
                float slideX = n.IsLeaving
                    ? 1f - EaseInCubic(n.LeaveProgress)
                    : EaseOutCubic(n.AnimationProgress);
                DrawNotification(canvas, n, (int)((slideX - 1f) * width), y, width, notifCardH);
                y += notifCardH + pad;
            }
        }

        return (bitmap.Bytes, width, height);
    }

    private void DrawJoinLeaveNotification(SKCanvas canvas, OverlayNotification n, int xOffset, int y, int width, int height)
    {
        float textY = y + height / 2f + 4f;
        float x = xOffset + AvatarMargin;

        using var namePaint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        canvas.DrawText(n.AuthorName, x, textY, _counterFont, namePaint);

        x += _counterPaint.MeasureText(n.AuthorName) + 4;
        using var actionPaint = new SKPaint { IsAntialias = true, Color = new SKColor(140, 140, 140) };
        canvas.DrawText(n.Content, x, textY, _counterFont, actionPaint);
    }

    private void DrawNotification(SKCanvas canvas, OverlayNotification n, int xOffset, int y, int width, int height)
    {
        var rect = new SKRect(xOffset + 4, y, xOffset + width - 4, y + height);
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(30, 31, 34, 200),
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawRoundRect(rect, CornerRadius, CornerRadius, bgPaint);

        int avX = xOffset + AvatarMargin;
        int avY = y + (height - AvatarSize) / 2;

        canvas.Save();
        using var clipPath = new SKPath();
        clipPath.AddCircle(avX + AvatarSize / 2f, avY + AvatarSize / 2f, AvatarSize / 2f);
        canvas.ClipPath(clipPath, antialias: true);
        if (n.AuthorAvatar != null)
        {
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(n.AuthorAvatar, new SKRect(avX, avY, avX + AvatarSize, avY + AvatarSize), paint);
        }
        else
        {
            using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(88, 101, 242) };
            canvas.DrawCircle(avX + AvatarSize / 2f, avY + AvatarSize / 2f, AvatarSize / 2f, paint);
        }
        canvas.Restore();

        float textX = xOffset + TextLeftMargin;
        using var namePaint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        canvas.DrawText(n.AuthorName, textX, y + 20, _nameFont, namePaint);

        string content = n.Content.Length > 50 ? n.Content[..50] + "..." : n.Content;
        canvas.DrawText(content, textX, y + 38, _counterFont, _counterPaint);
    }

    private void DrawUserCard(SKCanvas canvas, VoiceUser user, int xOffset, int y, int width, int height, bool isMutedSection)
    {
        var cardRect = new SKRect(xOffset + 4, y, xOffset + width - 4, y + height);

        if (user.IsSpeaking && !user.IsLeaving)
        {
            using var glowPaint = new SKPaint
            {
                IsAntialias = true,
                Color = SpeakingGreen,
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawRoundRect(cardRect, CornerRadius, CornerRadius, glowPaint);

            using var borderPaint = new SKPaint
            {
                IsAntialias = true,
                Color = SpeakingBorder,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
            };
            canvas.DrawRoundRect(cardRect, CornerRadius, CornerRadius, borderPaint);
        }

        int avatarX = xOffset + AvatarMargin;
        int avatarY = y + (height - AvatarSize) / 2;
        DrawCircleAvatar(canvas, user, avatarX, avatarY, AvatarSize);

        float textY = y + height / 2f + 5f;
        byte alpha = isMutedSection ? (byte)140 : (byte)255;
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = user.RoleColor.WithAlpha(alpha),
        };
        canvas.DrawText(user.DisplayName, xOffset + TextLeftMargin, textY, _nameFont, textPaint);

        int iconX = xOffset + width - IconRightMargin - IconSize - 8;
        int iconY = y + (height - IconSize) / 2;

        if (user.IsDeafened)
        {
            DrawDeafIcon(canvas, iconX, iconY, IconSize, user.ServerDeaf);
        }
        else if (user.IsMuted)
        {
            DrawMuteIcon(canvas, iconX, iconY, IconSize, user.ServerMute);
        }

        if (isMutedSection)
        {
            using var overlayPaint = new SKPaint
            {
                IsAntialias = true,
                Color = MutedOverlay,
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawRoundRect(cardRect, CornerRadius, CornerRadius, overlayPaint);
        }
    }

    private void DrawCircleAvatar(SKCanvas canvas, VoiceUser user, int x, int y, int size)
    {
        float cx = x + size / 2f;
        float cy = y + size / 2f;
        float radius = size / 2f;

        canvas.Save();

        using var clipPath = new SKPath();
        clipPath.AddCircle(cx, cy, radius);
        canvas.ClipPath(clipPath, antialias: true);

        if (user.AvatarBitmap != null)
        {
            var destRect = new SKRect(x, y, x + size, y + size);
            using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
            canvas.DrawBitmap(user.AvatarBitmap, destRect, paint);
        }
        else
        {
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(88, 101, 242), // Discord blurple
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawCircle(cx, cy, radius, paint);
        }

        canvas.Restore();

        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, 40),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
        };
        canvas.DrawCircle(cx, cy, radius, borderPaint);
    }

    private void DrawMuteIcon(SKCanvas canvas, int x, int y, int size, bool isServerMute)
    {
        var color = isServerMute ? new SKColor(237, 66, 69) : new SKColor(180, 180, 180);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            StrokeCap = SKStrokeCap.Round,
        };

        float cx = x + size / 2f;
        float cy = y + size / 2f;
        float r = size * 0.3f;

        canvas.DrawRoundRect(new SKRect(cx - r, cy - r * 1.5f, cx + r, cy + r * 0.5f), r, r, paint);

        using var arcPath = new SKPath();
        arcPath.AddArc(new SKRect(cx - r * 1.3f, cy - r, cx + r * 1.3f, cy + r * 1.2f), 0, 180);
        canvas.DrawPath(arcPath, paint);

        canvas.DrawLine(cx, cy + r * 1.2f, cx, cy + r * 1.8f, paint);

        using var slashPaint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            StrokeCap = SKStrokeCap.Round,
        };
        canvas.DrawLine(x + 2, y + size - 2, x + size - 2, y + 2, slashPaint);
    }

    private void DrawDeafIcon(SKCanvas canvas, int x, int y, int size, bool isServerDeaf)
    {
        var color = isServerDeaf ? new SKColor(237, 66, 69) : new SKColor(180, 180, 180);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            StrokeCap = SKStrokeCap.Round,
        };

        float cx = x + size / 2f;
        float cy = y + size / 2f;

        using var arcPath = new SKPath();
        arcPath.AddArc(new SKRect(x + 2, y + 2, x + size - 2, y + size - 2), 200, 140);
        canvas.DrawPath(arcPath, paint);

        float cupW = size * 0.18f;
        float cupH = size * 0.3f;
        canvas.DrawRoundRect(new SKRect(x + 1, cy, x + 1 + cupW, cy + cupH), 2, 2, paint);
        canvas.DrawRoundRect(new SKRect(x + size - 1 - cupW, cy, x + size - 1, cy + cupH), 2, 2, paint);

        canvas.DrawLine(x + 2, y + size - 2, x + size - 2, y + 2, paint);
    }

    private void DrawCounterText(SKCanvas canvas, string text, int y, int width)
    {
        float textWidth = _counterPaint.MeasureText(text);
        float x = (width - textWidth) / 2f;
        canvas.DrawText(text, x, y + 14, _counterFont, _counterPaint);
    }

    private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
    private static float EaseInCubic(float t) => t * t * t;
}
