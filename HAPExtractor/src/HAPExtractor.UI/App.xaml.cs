using System.Windows;
using System.Windows.Threading;
using HAPExtractor.Core.Services;
using HAPExtractor.Infrastructure.Firebase;
using HAPExtractor.Infrastructure.Services;

namespace HAPExtractor.UI;

public partial class App : Application
{
    private FirebaseLifecycleManager? _firebaseManager;
    private HapFirebaseService? _firebaseService;

    public FirebaseLifecycleManager? FirebaseManager => _firebaseManager;
    public HapFirebaseService? FirebaseService => _firebaseService;

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Logger.Info("HAP Extractor starting up");
        Logger.Info($"Log file: {Logger.LogFilePath}");

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        // Initialize Firebase tracking (non-blocking)
        InitializeFirebaseAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Shutdown Firebase lifecycle with a hard timeout to avoid hanging
        if (_firebaseManager != null)
        {
            try
            {
                var shutdownTask = _firebaseManager.ShutdownAsync();
                if (!shutdownTask.Wait(TimeSpan.FromSeconds(3)))
                    Logger.Warn("Firebase shutdown timed out after 3 seconds");
            }
            catch { /* swallow */ }
        }

        base.OnExit(e);

        // Force-terminate if background threads are still alive
        Environment.Exit(0);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void InitializeFirebaseAsync()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                Logger.Info("Firebase: Starting initialization in background...");

                _firebaseService = new HapFirebaseService();
                _firebaseManager = new FirebaseLifecycleManager(_firebaseService);

                var initTask = _firebaseManager.InitializeAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

                var completedTask = await Task.WhenAny(initTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Logger.Warn("Firebase: Initialization timed out after 10 seconds, running in offline mode");
                }
                else
                {
                    await initTask;
                    Logger.Info("Firebase: Initialization completed successfully");

                    // Wire forced update handler
                    if (_firebaseService.IsInitialized)
                    {
                        _firebaseService.ForcedUpdateDetected += async (sender, forcedInfo) =>
                        {
                            Logger.Info($"Forced update detected — v{forcedInfo.TargetVersion} pushed by {forcedInfo.PushedBy}");
                            try
                            {
                                await _firebaseService.UpdateForcedUpdateStatusAsync("downloading");

                                var updateInfo = new Infrastructure.Firebase.Models.UpdateInfo
                                {
                                    LatestVersion = forcedInfo.TargetVersion,
                                    CurrentVersion = (await _firebaseManager.CheckForUpdatesAsync())?.CurrentVersion ?? "0.0.0",
                                    DownloadUrl = forcedInfo.DownloadUrl,
                                    ReleaseNotes = $"Pushed by admin ({forcedInfo.PushedBy})",
                                    RequiredUpdate = true
                                };

                                await _firebaseService.UpdateForcedUpdateStatusAsync("installing");

                                // Dispatch to UI thread to perform the update
                                Current.Dispatcher.Invoke(() =>
                                {
                                    if (MainWindow is MainWindow mainWin)
                                        _ = mainWin.DownloadAndInstallUpdateAsync(updateInfo, silent: true);
                                });
                            }
                            catch (Exception fuEx)
                            {
                                Logger.Error("Forced update failed", fuEx);
                                await _firebaseService.UpdateForcedUpdateStatusAsync("failed", fuEx.Message);
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Firebase initialization failed", ex);
            }
        });
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Fatal("Unhandled UI exception", e.Exception);
        _ = _firebaseManager?.LogErrorAsync(e.Exception, "DispatcherUnhandledException");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails logged to:\n{Logger.LogFilePath}",
            "HAP Extractor Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.Fatal("Unhandled domain exception", ex);
            _ = _firebaseManager?.LogErrorAsync(ex, "UnhandledException");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Fatal("Unobserved task exception", e.Exception);
        _ = _firebaseManager?.LogErrorAsync(e.Exception, "UnobservedTaskException");
        e.SetObserved();
    }
}

