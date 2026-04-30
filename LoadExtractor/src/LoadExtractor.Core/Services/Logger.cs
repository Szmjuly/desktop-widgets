using System.Runtime.CompilerServices;

namespace LoadExtractor.Core.Services;

public static class Logger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LoadExtractor", "logs");

    private static readonly string LogFile;
    private static readonly object Lock = new();

    static Logger()
    {
        Directory.CreateDirectory(LogDir);
        LogFile = Path.Combine(LogDir, $"loadextractor_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    }

    public static string LogFilePath => LogFile;

    public static void Info(string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "")
    {
        Write("INFO", message, caller, file);
    }

    public static void Warn(string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "")
    {
        Write("WARN", message, caller, file);
    }

    public static void Error(string message, Exception? ex = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "")
    {
        var msg = ex != null ? $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}\n  Stack: {ex.StackTrace}" : message;
        Write("ERROR", msg, caller, file);
    }

    public static void Fatal(string message, Exception? ex = null,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "")
    {
        var msg = ex != null ? $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}\n  Stack: {ex.StackTrace}" : message;
        Write("FATAL", msg, caller, file);
    }

    private static void Write(string level, string message, string caller, string file)
    {
        try
        {
            var shortFile = Path.GetFileName(file);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{shortFile}::{caller}] {message}";
            lock (Lock)
            {
                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
        }
        catch
        {
            // Swallow logging errors to never crash the app
        }
    }
}
