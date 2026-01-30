using System.IO;
using System.Threading;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class App : System.Windows.Application
{
    private static bool _shownDispatcherCrash;
    private static Mutex? _instanceMutex;
    private const string MutexName = "Global\\DesktopHub_SingleInstance_Mutex";

    public App()
    {
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // Check if we need to self-install
        if (ShouldSelfInstall())
        {
            base.OnStartup(e);
            PerformSelfInstall();
            Current.Shutdown();
            return;
        }
        
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
            var settings = new DesktopHub.Infrastructure.Settings.SettingsService();
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
    
    private bool ShouldSelfInstall()
    {
        try
        {
            // For single-file apps, use Process.MainModule.FileName instead of Assembly.Location
            var currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExe))
            {
                DebugLogger.Log("ShouldSelfInstall: Cannot determine current executable path");
                return false;
            }
            
            var expectedLocalPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopHub",
                "DesktopHub.exe"
            );
            
            // If we're not running from the expected local location, we should install
            if (!currentExe.Equals(expectedLocalPath, StringComparison.OrdinalIgnoreCase))
            {
                // But don't install if we're already installed (user might have moved it)
                if (File.Exists(expectedLocalPath))
                {
                    var currentVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(currentExe);
                    var installedVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(expectedLocalPath);
                    
                    // If versions are the same, don't reinstall
                    if (currentVersion.FileVersion == installedVersion.FileVersion)
                    {
                        DebugLogger.Log($"ShouldSelfInstall: Already installed at {expectedLocalPath}");
                        return false;
                    }
                }
                
                DebugLogger.Log($"ShouldSelfInstall: Need to install from {currentExe} to {expectedLocalPath}");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"ShouldSelfInstall: Error checking install status: {ex.Message}");
            return false;
        }
    }
    
    private void PerformSelfInstall()
    {
        try
        {
            var currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopHub"
            );
            var targetExe = Path.Combine(installDir, "DesktopHub.exe");
            
            DebugLogger.Log($"PerformSelfInstall: Installing from {currentExe} to {targetExe}");
            
            // Create install directory
            if (!Directory.Exists(installDir))
            {
                Directory.CreateDirectory(installDir);
                DebugLogger.Log($"PerformSelfInstall: Created directory {installDir}");
            }
            
            // Copy executable
            File.Copy(currentExe, targetExe, overwrite: true);
            DebugLogger.Log($"PerformSelfInstall: Copied executable");
            
            // Create Start Menu shortcut
            CreateStartMenuShortcut(targetExe);
            
            // Add to startup
            AddToStartup(targetExe);
            
            // Launch the installed copy
            DebugLogger.Log($"PerformSelfInstall: Launching installed copy");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = targetExe,
                UseShellExecute = true
            });
            
            System.Windows.MessageBox.Show(
                "DesktopHub has been installed successfully!\n\n" +
                $"Location: {installDir}\n\n" +
                "The application will now start and run in the background.\n" +
                "Look for the tray icon or press Ctrl+Alt+Space to open.",
                "DesktopHub Installed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"PerformSelfInstall: Error during installation: {ex}");
            System.Windows.MessageBox.Show(
                $"Failed to install DesktopHub:\n{ex.Message}\n\n" +
                "You can still run the application from its current location.",
                "Installation Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning
            );
        }
    }
    
    private void CreateStartMenuShortcut(string targetExe)
    {
        try
        {
            var startMenuDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs", "DesktopHub"
            );
            
            if (!Directory.Exists(startMenuDir))
            {
                Directory.CreateDirectory(startMenuDir);
            }
            
            var shortcutPath = Path.Combine(startMenuDir, "DesktopHub.lnk");
            
            // Create shortcut using IWshRuntimeLibrary (Windows Script Host)
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                DebugLogger.Log("PerformSelfInstall: WScript.Shell not available");
                return;
            }
            
            var shell = Activator.CreateInstance(shellType);
            if (shell == null)
            {
                DebugLogger.Log("PerformSelfInstall: Failed to create WScript.Shell instance");
                return;
            }
            
            var shortcut = shell.GetType().InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            
            if (shortcut != null)
            {
                shortcut.GetType().InvokeMember("TargetPath",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
                shortcut.GetType().InvokeMember("WorkingDirectory",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetExe) ?? "" });
                shortcut.GetType().InvokeMember("Description",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "DesktopHub - Quick Project Search" });
                shortcut.GetType().InvokeMember("Save",
                    System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
                
                DebugLogger.Log($"PerformSelfInstall: Created Start Menu shortcut at {shortcutPath}");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"PerformSelfInstall: Failed to create Start Menu shortcut: {ex.Message}");
        }
    }
    
    private void AddToStartup(string targetExe)
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true))
            {
                if (key != null)
                {
                    key.SetValue("DesktopHub", $"\"{targetExe}\"");
                    DebugLogger.Log("PerformSelfInstall: Added to Windows Startup");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"PerformSelfInstall: Failed to add to startup: {ex.Message}");
        }
    }
    
    private static void ActivateExistingInstance()
    {
        try
        {
            // Find the existing DesktopHub window
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
        
        // Handle the specific "Cannot set Visibility while closing" WPF bug gracefully
        if (e.Exception is InvalidOperationException ioe && 
            ioe.Message.Contains("Cannot set Visibility") && 
            ioe.Message.Contains("while a Window is closing"))
        {
            DebugLogger.Log("OnDispatcherUnhandledException: Caught window closing race condition - continuing without crash");
            e.Handled = true;
            return; // Don't shutdown the app for this known WPF bug
        }
        
        // For other exceptions, show error and shutdown
        if (!_shownDispatcherCrash)
        {
            _shownDispatcherCrash = true;
            System.Windows.MessageBox.Show(
                $"A fatal error occurred. Check logs\\debug.log for details.\n{e.Exception.Message}",
                "DesktopHub",
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
