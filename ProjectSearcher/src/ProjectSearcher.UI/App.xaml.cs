using System.IO;
using ProjectSearcher.UI.Helpers;

namespace ProjectSearcher.UI;

public partial class App : System.Windows.Application
{
    private static bool _shownDispatcherCrash;

    public App()
    {
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Create Start Menu shortcut on startup (only creates if it doesn't exist)
        StartMenuHelper.CreateStartMenuShortcut();
    }

    private static void LogCrash(string title, Exception ex)
    {
        try
        {
            DebugLogger.Log($"CRASH - {title}: {ex.Message}");
            DebugLogger.Log($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                DebugLogger.Log($"Inner exception: {ex.InnerException.Message}");
                DebugLogger.Log($"Inner stack trace: {ex.InnerException.StackTrace}");
            }
        }
        catch { }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash("DispatcherUnhandledException", e.Exception);
        if (!_shownDispatcherCrash)
        {
            _shownDispatcherCrash = true;
            System.Windows.MessageBox.Show(
                $"A fatal error occurred. Check debug.txt for details.\n{e.Exception.Message}",
                "Project Searcher",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
        }
        e.Handled = true;
        try { Current.Shutdown(); } catch { }
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrash("UnhandledException", ex);
        }
    }
}
