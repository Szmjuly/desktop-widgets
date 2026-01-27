using System.IO;
using System.Threading;
using ProjectSearcher.UI.Helpers;

namespace ProjectSearcher.UI;

public partial class App : System.Windows.Application
{
    private static bool _shownDispatcherCrash;
    private static Mutex? _instanceMutex;
    private const string MutexName = "Global\\ProjectSearcher_SingleInstance_Mutex";

    public App()
    {
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // Check if another instance is already running
        bool createdNew;
        _instanceMutex = new Mutex(true, MutexName, out createdNew);
        
        if (!createdNew)
        {
            // Initialize WPF for dialog display
            base.OnStartup(e);
            
            // Another instance is already running
            DebugLogger.Log("OnStartup: Another instance is already running, showing dialog");
            
            // Load settings to get hotkey
            var settings = new ProjectSearcher.Infrastructure.Settings.SettingsService();
            settings.LoadAsync().Wait();
            var (modifiers, key) = settings.GetHotkey();
            var hotkeyLabel = FormatHotkey(modifiers, key);
            
            // Show "Already Running" dialog
            var action = AlreadyRunningDialog.Show(hotkeyLabel);
            DebugLogger.Log($"OnStartup: User selected action: {action}");
            
            // Shutdown this instance
            Current.Shutdown();
            return;
        }
        
        DebugLogger.Log("OnStartup: First instance, starting application");
        
        base.OnStartup(e);
        
        // Create Start Menu shortcut on startup (only creates if it doesn't exist)
        StartMenuHelper.CreateStartMenuShortcut();
        
        // Manually create SearchOverlay (it will hide itself after initialization)
        var mainWindow = new SearchOverlay();
        MainWindow = mainWindow;
        mainWindow.Show(); // Must show to trigger Window_Loaded which initializes tray/hotkey
    }
    
    private static void ActivateExistingInstance()
    {
        try
        {
            // Find the existing ProjectSearcher window
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
            
            foreach (var process in processes)
            {
                // Skip current process
                if (process.Id == currentProcess.Id)
                    continue;
                
                // Found another instance - activate its main window
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    // Restore if minimized
                    ShowWindow(process.MainWindowHandle, SW_RESTORE);
                    // Bring to foreground
                    SetForegroundWindow(process.MainWindowHandle);
                    DebugLogger.Log($"ActivateExistingInstance: Activated existing window (PID: {process.Id})");
                    return;
                }
            }
            
            DebugLogger.Log("ActivateExistingInstance: No existing window found to activate");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"ActivateExistingInstance: Error: {ex.Message}");
        }
    }
    
    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        base.OnExit(e);
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
    }
    
    // Win32 API imports for window activation
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    private const int SW_RESTORE = 9;

    private static string FormatHotkey(int modifiers, int key)
    {
        var parts = new List<string>();

        if ((modifiers & 0x0002) != 0) // MOD_CONTROL
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & 0x0001) != 0) // MOD_ALT
        {
            parts.Add("Alt");
        }

        if ((modifiers & 0x0004) != 0) // MOD_SHIFT
        {
            parts.Add("Shift");
        }

        if ((modifiers & 0x0008) != 0) // MOD_WIN
        {
            parts.Add("Win");
        }

        var keyLabel = System.Windows.Input.KeyInterop.KeyFromVirtualKey(key);
        var keyText = keyLabel != System.Windows.Input.Key.None ? keyLabel.ToString() : $"0x{key:X}";
        parts.Add(keyText);

        return string.Join("+", parts);
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
