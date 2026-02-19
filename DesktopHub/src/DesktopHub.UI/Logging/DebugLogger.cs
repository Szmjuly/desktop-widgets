using System;
using System.IO;

namespace DesktopHub.UI;

public static class DebugLogger
{
#if DEBUG
    private static readonly string LogDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "logs"
    );
    
    private static readonly string LogPath = Path.Combine(LogDirectory, "debug.log");
    
    private static readonly object _lock = new object();
#endif
    
    static DebugLogger()
    {
#if DEBUG
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }
        catch
        {
            // Ignore directory creation errors
        }
#endif
    }

    public static void Log(string message)
    {
#if DEBUG
        try
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var logLine = $"[{timestamp}] {message}";
                File.AppendAllText(LogPath, logLine + Environment.NewLine);
                System.Diagnostics.Debug.WriteLine(logLine);
            }
        }
        catch
        {
            // Ignore logging errors
        }
#endif
    }

    public static void LogSeparator(string title = "")
    {
#if DEBUG
        try
        {
            lock (_lock)
            {
                var separator = new string('=', 80);
                File.AppendAllText(LogPath, Environment.NewLine + separator + Environment.NewLine);
                if (!string.IsNullOrEmpty(title))
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    var titleLine = $"[{timestamp}] === {title} ===";
                    File.AppendAllText(LogPath, titleLine + Environment.NewLine);
                }
                File.AppendAllText(LogPath, separator + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore logging errors
        }
#endif
    }

    public static void LogHeader(string header)
    {
#if DEBUG
        try
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var headerLine = $"\n[{timestamp}] *** {header} ***";
                File.AppendAllText(LogPath, headerLine + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore logging errors
        }
#endif
    }

    public static void LogVariable(string name, object? value)
    {
#if DEBUG
        try
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var valueStr = value?.ToString() ?? "<null>";
                var logLine = $"[{timestamp}]   {name} = {valueStr}";
                File.AppendAllText(LogPath, logLine + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore logging errors
        }
#endif
    }

    public static void Clear()
    {
#if DEBUG
        try
        {
            lock (_lock)
            {
                if (File.Exists(LogPath))
                {
                    File.Delete(LogPath);
                }
                Log("=== NEW SESSION ===");
            }
        }
        catch
        {
            // Ignore
        }
#endif
    }
}
