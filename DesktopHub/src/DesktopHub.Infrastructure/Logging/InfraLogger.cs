using System;
using System.IO;

namespace DesktopHub.Infrastructure.Logging;

public static class InfraLogger
{
    private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "debug.log");
    private static readonly object _lock = new object();

    public static void Log(string message)
    {
        try
        {
            lock (_lock)
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var logLine = $"[{timestamp}] {message}";

                File.AppendAllText(LogFilePath, logLine + Environment.NewLine);
                
                // Also write to console for IDE debugging
                Console.WriteLine(logLine);
            }
        }
        catch
        {
            // Silently fail if logging fails
        }
    }
}
