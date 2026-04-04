using VRDiscordOverlay.Web;

namespace VRDiscordOverlay;

public static class ConsoleUI
{
    private static string _statusLine = "";
    private static readonly object _lock = new();
    private static WebServer? _webServer;

    public static void Init(string statusLine, WebServer? webServer = null)
    {
        _statusLine = statusLine;
        _webServer = webServer;
        Console.CursorVisible = false;
        RedrawStatus();
    }

    public static void Log(string message)
    {
        lock (_lock)
        {
            ClearStatusLine();
            Console.WriteLine(message);
            RedrawStatus();
        }
        _webServer?.BroadcastLog(message);
    }

    public static void SetStatus(string statusLine)
    {
        lock (_lock)
        {
            _statusLine = statusLine;
            ClearStatusLine();
            RedrawStatus();
        }
    }

    public static void BroadcastState(object state)
    {
        _webServer?.BroadcastState(state);
    }

    private static void ClearStatusLine()
    {
        try
        {
            int y = Console.WindowHeight - 1;
            Console.SetCursorPosition(0, y);
            Console.Write(new string(' ', Console.WindowWidth - 1));
            Console.SetCursorPosition(0, y);
        }
        catch { }
    }

    private static void RedrawStatus()
    {
        try
        {
            int y = Console.WindowHeight - 1;
            int savedTop = Console.CursorTop;
            int savedLeft = Console.CursorLeft;

            Console.SetCursorPosition(0, y);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            var text = _statusLine.Length > Console.WindowWidth - 1
                ? _statusLine[..(Console.WindowWidth - 1)]
                : _statusLine;
            Console.Write(text);
            Console.ResetColor();

            int logY = Math.Min(savedTop, y - 1);
            Console.SetCursorPosition(savedLeft, logY);
        }
        catch { }
    }
}
