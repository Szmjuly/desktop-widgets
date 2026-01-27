using System;
using System.IO;

namespace ProjectSearcher.UI;

public static class DebugLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "ProjectSearcher_Debug.log"
    );
    
    private static readonly object _lock = new object();

    public static void Log(string message)
    {
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
    }

    public static void LogSeparator(string title = "")
    {
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
    }

    public static void LogHeader(string header)
    {
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
    }

    public static void LogVariable(string name, object? value)
    {
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
    }

    public static void Clear()
    {
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
    }
}
