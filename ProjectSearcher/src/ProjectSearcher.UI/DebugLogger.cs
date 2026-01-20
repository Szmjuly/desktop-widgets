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
