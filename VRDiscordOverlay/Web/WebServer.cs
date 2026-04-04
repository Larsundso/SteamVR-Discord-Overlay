using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VRDiscordOverlay.Config;

namespace VRDiscordOverlay.Web;

public class WebServer
{
    private WebApplication? _app;
    private readonly AppSettings _settings;
    private readonly List<WebSocket> _clients = new();
    private readonly List<string> _logBuffer = new();
    private readonly object _lock = new();
    private object? _lastState;
    private const int MaxLogBuffer = 200;
    public int Port { get; private set; }

    public event Action<string, string?>? OnCommand;

    public WebServer(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        Port = 39039;
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://localhost:{Port}");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        _app = builder.Build();

        _app.UseWebSockets();

        _app.MapGet("/", () => Results.Content(DashboardHtml.Page, "text/html"));

        _app.MapGet("/api/settings", () => Results.Text(
            JsonConvert.SerializeObject(_settings), "application/json"));

        _app.MapPost("/api/settings", async (HttpContext ctx) =>
        {
            var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            var patch = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            if (patch == null) return Results.BadRequest();

            foreach (var kv in patch)
            {
                var prop = typeof(AppSettings).GetProperty(kv.Key);
                if (prop == null || !prop.CanWrite) continue;

                var val = Convert.ChangeType(
                    kv.Value is Newtonsoft.Json.Linq.JToken jt ? jt.ToObject(prop.PropertyType) : kv.Value,
                    prop.PropertyType);
                prop.SetValue(_settings, val);
            }

            SettingsManager.Save(_settings);
            OnCommand?.Invoke("settings_changed", null);
            return Results.Ok();
        });

        _app.MapPost("/api/command/{name}", async (string name, HttpContext ctx) =>
        {
            string? arg = null;
            if (ctx.Request.ContentLength > 0)
                arg = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            OnCommand?.Invoke(name, arg);
            return Results.Ok();
        });

        _app.Map("/ws", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }

            var ws = await ctx.WebSockets.AcceptWebSocketAsync();

            string[] logs;
            lock (_lock)
            {
                _clients.Add(ws);
                logs = _logBuffer.ToArray();
            }

            foreach (var log in logs)
            {
                var logJson = JsonConvert.SerializeObject(new { type = "log", message = log });
                await ws.SendAsync(Encoding.UTF8.GetBytes(logJson), WebSocketMessageType.Text, true, ct);
            }

            if (_lastState != null)
            {
                var stateJson = JsonConvert.SerializeObject(new { type = "state", data = _lastState });
                await ws.SendAsync(Encoding.UTF8.GetBytes(stateJson), WebSocketMessageType.Text, true, ct);
            }

            var buf = new byte[1024];
            try
            {
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(buf, ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch { }
            finally
            {
                lock (_lock) { _clients.Remove(ws); }
            }
        });

        await _app.StartAsync(ct);
    }

    public void BroadcastLog(string message)
    {
        lock (_lock)
        {
            _logBuffer.Add(message);
            if (_logBuffer.Count > MaxLogBuffer)
                _logBuffer.RemoveAt(0);
        }
        var json = JsonConvert.SerializeObject(new { type = "log", message });
        Broadcast(json);
    }

    public void BroadcastState(object state)
    {
        _lastState = state;
        var json = JsonConvert.SerializeObject(new { type = "state", data = state });
        Broadcast(json);
    }

    private void Broadcast(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        lock (_lock)
        {
            foreach (var ws in _clients.ToArray())
            {
                if (ws.State == WebSocketState.Open)
                {
                    _ = ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }
}
