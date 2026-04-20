using System.Diagnostics;
using System.IO;
using System.Threading;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;
using DesktopHub.Infrastructure.Firebase;
using DesktopHub.Infrastructure.Data;
using DesktopHub.Infrastructure.Telemetry;

namespace DesktopHub.UI;

public partial class App : System.Windows.Application
{
    private static bool _shownDispatcherCrash;
    private static Mutex? _instanceMutex;
    private const string MutexName = "Global\\DesktopHub_SingleInstance_Mutex";
    private FirebaseLifecycleManager? _firebaseManager;
    private FirebaseService? _firebaseService;
    private TelemetryService? _telemetryService;
    private ThemeService? _themeService;
    private readonly Stopwatch _startupStopwatch = Stopwatch.StartNew();

    public FirebaseLifecycleManager? FirebaseManager => _firebaseManager;
    public ITelemetryService? Telemetry => _telemetryService;
    public ThemeService? Theme => _themeService;

    public App()
    {
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // Check if we need to self-install
        if (ShouldSelfInstall())
        {
            base.OnStartup(e);
            bool installSucceeded = PerformSelfInstall();
            if (installSucceeded)
            {
                Current.Shutdown();
                return;
            }
            // If install failed, continue running from current location
            DebugLogger.Log("OnStartup: Install failed, continuing from current location");
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
        
        // Initialize Firebase tracking (non-blocking)
        InitializeFirebaseAsync();
        
        // Initialize telemetry (non-blocking)
        InitializeTelemetryAsync();
        
        // Create Start Menu shortcut on startup (only creates if it doesn't exist)
        StartMenuHelper.CreateStartMenuShortcut();
        
        // Initialize theme service early (before any UI is created)
        // NOTE: Cannot use LoadAsync().Wait() here — it deadlocks the WPF dispatcher.
        // Use LoadSync() or a fresh SettingsService that loads synchronously.
        var themeSettings = new DesktopHub.Infrastructure.Settings.SettingsService();
        themeSettings.LoadSync();
        _themeService = new ThemeService(themeSettings);
        _themeService.Initialize();

        // Phase 2: first-run preset picker. Shown once per install when ScanProfiles is empty
        // AND the user has never completed setup. Legacy CES users are auto-migrated on load and
        // marked complete, so they never see this.
        if (!themeSettings.GetHasCompletedFirstRun() && themeSettings.GetScanProfiles().Count == 0)
        {
            try
            {
                var (preset, skipped) = WelcomeWizard.Show();
                if (preset.HasValue)
                {
                    themeSettings.SetScanProfiles(ScanProfilePresets.ForPresetId(preset.Value));
                    DebugLogger.Log($"WelcomeWizard: Applied preset {preset.Value} ({themeSettings.GetScanProfiles().Count} profile(s))");
                }
                else if (skipped)
                {
                    DebugLogger.Log("WelcomeWizard: User skipped — no profiles applied");
                }
                themeSettings.SetHasCompletedFirstRun(true);
                themeSettings.SaveAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"WelcomeWizard: Error showing wizard: {ex.Message}");
            }
        }

        // Manually create SearchOverlay (it will hide itself after initialization)
        var mainWindow = new SearchOverlay();
        MainWindow = mainWindow;
        mainWindow.Show(); // Must show to trigger Window_Loaded which initializes tray/hotkey
        
        // Track startup timing
        var startupMs = _startupStopwatch.ElapsedMilliseconds;
        DebugLogger.Log($"App startup completed in {startupMs}ms");
        TelemetryAccessor.TrackPerformance(TelemetryEventType.StartupTiming, "app_startup", startupMs);
    }
    
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void InitializeFirebaseAsync()
    {
        DebugLogger.Log("App.xaml.cs: InitializeFirebaseAsync CALLED");
        
        // Fire-and-forget with timeout to avoid blocking app startup
        _ = Task.Run(async () =>
        {
            try
            {
                DebugLogger.Log("Firebase: Starting initialization in background...");
                DebugLogger.Log("Firebase: Creating FirebaseService instance...");
                
                _firebaseService = new FirebaseService();
                DebugLogger.Log("Firebase: Creating FirebaseLifecycleManager instance...");
                
                _firebaseManager = new FirebaseLifecycleManager(_firebaseService);
                DebugLogger.Log("Firebase: Calling InitializeAsync...");
                
                // Add 10 second timeout for initialization
                var initTask = _firebaseManager.InitializeAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                
                var completedTask = await Task.WhenAny(initTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    DebugLogger.Log("Firebase: Initialization timed out after 10 seconds, running in offline mode");
                }
                else
                {
                    await initTask; // Propagate any exceptions
                    DebugLogger.Log("Firebase: Initialization completed successfully");
                    
                    // Connect Firebase to telemetry for periodic sync
                    if (_telemetryService != null && _firebaseService.IsInitialized)
                    {
                        _telemetryService.SetFirebaseService(_firebaseService);
                    }

                    // Wire forced update handler — when heartbeat detects admin push, trigger silent update
                    if (_firebaseService.IsInitialized)
                    {
                        _firebaseService.ForcedUpdateDetected += async (sender, forcedInfo) =>
                        {
                            DebugLogger.Log($"App: Forced update detected — v{forcedInfo.TargetVersion} pushed by {forcedInfo.PushedBy}");
                            try
                            {
                                await _firebaseService.UpdateForcedUpdateStatusAsync("downloading");

                                var updateInfo = new Infrastructure.Firebase.Models.UpdateInfo
                                {
                                    LatestVersion = forcedInfo.TargetVersion,
                                    CurrentVersion = _firebaseManager != null
                                        ? (await _firebaseManager.CheckForUpdatesAsync())?.CurrentVersion ?? "0.0.0"
                                        : "0.0.0",
                                    DownloadUrl = forcedInfo.DownloadUrl,
                                    ReleaseNotes = $"Pushed by admin ({forcedInfo.PushedBy})",
                                    RequiredUpdate = true
                                };

                                // Get TrayIcon from SearchOverlay
                                TrayIcon? trayIcon = null;
                                Current.Dispatcher.Invoke(() =>
                                {
                                    if (MainWindow is SearchOverlay overlay)
                                        trayIcon = overlay.TrayIconInstance;
                                });

                                if (trayIcon != null)
                                {
                                    await _firebaseService.UpdateForcedUpdateStatusAsync("installing");
                                    await trayIcon.DownloadAndInstallUpdateAsync(updateInfo, silent: true);
                                }
                                else
                                {
                                    DebugLogger.Log("App: Forced update — TrayIcon not available");
                                    await _firebaseService.UpdateForcedUpdateStatusAsync("failed", "TrayIcon not available");
                                }
                            }
                            catch (Exception fuEx)
                            {
                                DebugLogger.Log($"App: Forced update failed: {fuEx.Message}");
                                await _firebaseService.UpdateForcedUpdateStatusAsync("failed", fuEx.Message);
                            }
                        };
                    }
                    
                    // Check metrics viewer feature flag and update launcher button
                    if (_firebaseService.IsInitialized)
                    {
                        try
                        {
                            var metricsEnabled = await _firebaseService.IsMetricsViewerEnabledAsync();
                            DebugLogger.Log($"Firebase: metrics_viewer_enabled = {metricsEnabled}");
                            if (metricsEnabled)
                            {
                                _ = Current.Dispatcher.BeginInvoke(() =>
                                {
                                    if (MainWindow is SearchOverlay overlay)
                                    {
                                        overlay.SetMetricsViewerEnabled(true);
                                    }
                                });
                            }
                        }
                        catch (Exception flagEx)
                        {
                            DebugLogger.Log($"Firebase: Feature flag check failed: {flagEx.Message}");
                        }
                    }

                    // Check DEV role and update Developer Panel visibility
                    if (_firebaseService.IsInitialized)
                    {
                        try
                        {
                            var isDev = await _firebaseService.IsUserDevAsync();
                            DebugLogger.Log($"Firebase: DEV role for current user = {isDev}");
                            _ = Current.Dispatcher.BeginInvoke(() =>
                            {
                                if (MainWindow is SearchOverlay overlay)
                                {
                                    overlay.SetDeveloperPanelEnabled(isDev);
                                }
                            });
                        }
                        catch (Exception devEx)
                        {
                            DebugLogger.Log($"Firebase: DEV role check failed: {devEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Firebase: Initialization EXCEPTION: {ex.Message}");
                DebugLogger.Log($"Firebase: Exception type: {ex.GetType().Name}");
                DebugLogger.Log($"Firebase: Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    DebugLogger.Log($"Firebase: Inner exception: {ex.InnerException.Message}");
                }
            }
        });
        
        DebugLogger.Log("App.xaml.cs: InitializeFirebaseAsync RETURNING (Task.Run started)");
    }
    
    private void InitializeTelemetryAsync()
    {
        DebugLogger.Log("App.xaml.cs: InitializeTelemetryAsync CALLED");
        
        _ = Task.Run(async () =>
        {
            try
            {
                var metricsDb = new MetricsDatabase();
                // Pass Firebase service for periodic sync (may be null if Firebase hasn't initialized yet)
                _telemetryService = new TelemetryService(metricsDb);
                await _telemetryService.InitializeAsync();
                
                // Connect Firebase if it's already initialized
                if (_firebaseService != null && _firebaseService.IsInitialized)
                {
                    _telemetryService.SetFirebaseService(_firebaseService);
                }
                
                // Make telemetry available to all widgets via static accessor
                TelemetryAccessor.Initialize(_telemetryService);
                
                DebugLogger.Log("Telemetry: Initialized successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Telemetry: Initialization failed: {ex.Message}");
            }
        });
    }
    
    private bool ShouldSelfInstall()
    {
        try
        {
#if DEBUG
            DebugLogger.Log("ShouldSelfInstall: Skipping (Debug build)");
            return false;
#else
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
#endif
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"ShouldSelfInstall: Error checking install status: {ex.Message}");
            return false;
        }
    }

    // Single-file publishes extract to a temp dir at runtime and leave Assembly.Location empty.
    // Multi-file publishes expose the assembly alongside the exe.
    private static bool IsSingleFilePublish()
    {
        try
        {
            var loc = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return string.IsNullOrEmpty(loc);
        }
        catch
        {
            return false;
        }
    }
    
    private bool PerformSelfInstall()
    {
        var currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
        var installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopHub"
        );
        var targetExe = Path.Combine(installDir, "DesktopHub.exe");
        
        try
        {
            DebugLogger.Log($"PerformSelfInstall: Installing from {currentExe} to {targetExe}");

            // Create install directory
            if (!Directory.Exists(installDir))
            {
                Directory.CreateDirectory(installDir);
                DebugLogger.Log($"PerformSelfInstall: Created directory {installDir}");
            }

            if (IsSingleFilePublish())
            {
                // Single-file publish: one exe is all we need.
                File.Copy(currentExe, targetExe, overwrite: true);
                DebugLogger.Log("PerformSelfInstall: Copied single-file exe");
            }
            else
            {
                // Multi-file publish: copy the entire directory so runtime/app DLLs come along.
                var sourceDir = Path.GetDirectoryName(currentExe) ?? "";
                DebugLogger.Log($"PerformSelfInstall: Multi-file publish detected, copying directory {sourceDir}");
                CopyDirectoryRecursive(sourceDir, installDir);
                // Ensure the final exe lives at the canonical "DesktopHub.exe" name even if the
                // source exe had a different name (e.g. DesktopHub.UI.exe during experimentation).
                var copiedExe = Path.Combine(installDir, Path.GetFileName(currentExe));
                if (!copiedExe.Equals(targetExe, StringComparison.OrdinalIgnoreCase) && File.Exists(copiedExe))
                {
                    File.Copy(copiedExe, targetExe, overwrite: true);
                }
                DebugLogger.Log("PerformSelfInstall: Copied multi-file publish directory");
            }
        }
        catch (Exception ex)
        {
            // Critical failure - cannot copy executable
            DebugLogger.Log($"PerformSelfInstall: CRITICAL ERROR - Failed to copy executable: {ex}");
            System.Windows.MessageBox.Show(
                $"Failed to install DesktopHub:\n{ex.Message}\n\n" +
                "Cannot copy executable to installation directory.\n" +
                "You can still run the application from its current location.",
                "Installation Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
            return false;
        }
        
        // Non-critical steps - log failures but continue
        var warnings = new List<string>();
        
        // Create Start Menu shortcut (non-critical)
        try
        {
            CreateStartMenuShortcut(targetExe);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"PerformSelfInstall: Failed to create Start Menu shortcut: {ex.Message}");
            warnings.Add("Start Menu shortcut could not be created");
        }
        
        // Add to startup (non-critical)
        try
        {
            AddToStartup(targetExe);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"PerformSelfInstall: Failed to add to startup: {ex.Message}");
            warnings.Add("Auto-start registry entry could not be created");
        }
        
        // Launch the installed copy
        bool launchSucceeded = true;
        try
        {
            DebugLogger.Log($"PerformSelfInstall: Launching installed copy");
            var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = targetExe,
                UseShellExecute = true
            });

            // If the child exits within 3s it almost certainly failed to start (missing DLLs,
            // runtime mismatch, etc.) — surface that instead of silently exiting this instance.
            if (child != null && child.WaitForExit(3000))
            {
                var exitCode = child.ExitCode;
                DebugLogger.Log($"PerformSelfInstall: Installed copy exited early with code {exitCode}");
                var logHint = Path.Combine(installDir, "logs", "debug.log");
                System.Windows.MessageBox.Show(
                    $"DesktopHub installed to:\n{installDir}\n\n" +
                    $"but the installed copy failed to start (exit code {exitCode}).\n\n" +
                    $"Log (if Debug build): {logHint}\n\n" +
                    "The current session will continue running from its original location.",
                    "Installed Copy Failed to Start",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning
                );
                // Return false so OnStartup keeps running from the current location instead of
                // calling Current.Shutdown(). Also skip the final "installed successfully" dialog.
                return false;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"PerformSelfInstall: Failed to launch installed copy: {ex.Message}");
            warnings.Add("Could not automatically launch installed application");
            launchSucceeded = false;
        }
        
        // Show success message with any warnings
        var message = "DesktopHub has been installed successfully!\n\n" +
                      $"Location: {installDir}\n\n";
        
        if (launchSucceeded)
        {
            message += "The application is now running in the background.\n" +
                      "Look for the tray icon or press Ctrl+Alt+Space to open.";
        }
        else
        {
            message += "Please launch DesktopHub from the Start Menu or:\n" +
                      $"{targetExe}";
        }
        
        if (warnings.Count > 0)
        {
            message += "\n\nNote: Some optional features could not be configured:\n" +
                      string.Join("\n", warnings.Select(w => $"• {w}"));
        }
        
        System.Windows.MessageBox.Show(
            message,
            "DesktopHub Installed",
            System.Windows.MessageBoxButton.OK,
            warnings.Count > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information
        );
        
        return launchSucceeded;
    }
    
    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, dir);
            var destSub = Path.Combine(targetDir, relative);
            if (!Directory.Exists(destSub))
            {
                Directory.CreateDirectory(destSub);
            }
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(targetDir, relative);
            try
            {
                File.Copy(file, destFile, overwrite: true);
            }
            catch (IOException ex)
            {
                // File in use (e.g. we're overwriting our own running install). Log and keep going.
                DebugLogger.Log($"CopyDirectoryRecursive: Skipped '{relative}' ({ex.Message})");
            }
            catch (UnauthorizedAccessException ex)
            {
                DebugLogger.Log($"CopyDirectoryRecursive: Access denied on '{relative}' ({ex.Message})");
            }
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
        // End telemetry session first
        if (_telemetryService != null)
        {
            try
            {
                _telemetryService.EndSessionAsync().Wait(TimeSpan.FromSeconds(5));
                _telemetryService.Dispose();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Telemetry shutdown error: {ex.Message}");
            }
        }
        
        if (_firebaseManager != null)
        {
            try
            {
                _firebaseManager.ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Firebase shutdown error: {ex.Message}");
            }
        }
        
        _themeService?.Dispose();
        
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

    private static string FormatHotkey(int modifiers, int key) =>
        HotkeyFormatter.FormatHotkey(modifiers, key);

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
        
        // Track error in telemetry
        TelemetryAccessor.TrackError(
            TelemetryEventType.AppError,
            e.Exception.GetType().Name,
            "DispatcherUnhandledException",
            e.Exception.Message);
        
        // Log to Firebase (non-blocking)
        if (_firebaseManager != null)
        {
            Task.Run(async () => await _firebaseManager.LogErrorAsync(e.Exception, "DispatcherUnhandledException"));
        }
        
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

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrash("UnhandledException", ex);
            
            // Track error in telemetry
            TelemetryAccessor.TrackError(
                TelemetryEventType.AppError,
                ex.GetType().Name,
                "UnhandledException",
                ex.Message);
            
            // Log to Firebase (non-blocking)
            if (_firebaseManager != null)
            {
                Task.Run(async () => await _firebaseManager.LogErrorAsync(ex, "UnhandledException"));
            }
        }
    }
}
