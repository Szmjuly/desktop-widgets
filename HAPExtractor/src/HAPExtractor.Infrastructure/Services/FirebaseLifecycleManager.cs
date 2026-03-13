using HAPExtractor.Infrastructure.Firebase;
using HAPExtractor.Infrastructure.Firebase.Models;

namespace HAPExtractor.Infrastructure.Services;

public class FirebaseLifecycleManager
{
    private readonly IHapFirebaseService _firebaseService;
    private readonly DateTime _startTime;
    private readonly string _appVersion;

    public IHapFirebaseService FirebaseService => _firebaseService;

    public FirebaseLifecycleManager(IHapFirebaseService firebaseService)
    {
        _firebaseService = firebaseService;
        _startTime = DateTime.UtcNow;
        _appVersion = GetAppVersion();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task InitializeAsync()
    {
        try
        {
            await _firebaseService.InitializeAsync();

            if (_firebaseService.IsInitialized)
            {
                await _firebaseService.RegisterDeviceAsync();
                await _firebaseService.LogAppLaunchAsync(_appVersion);
                _firebaseService.StartHeartbeat();

                // Check if a forced update was in progress before restart
                await _firebaseService.CompleteForcedUpdateIfPendingAsync();

                Log("Lifecycle manager initialized successfully");
            }
            else
            {
                Log("Not initialized, running in offline mode");
            }
        }
        catch (Exception ex)
        {
            Log($"Initialization error: {ex.Message}");
        }
    }

    public async Task ShutdownAsync()
    {
        try
        {
            _firebaseService.StopHeartbeat();

            var sessionDuration = DateTime.UtcNow - _startTime;
            await _firebaseService.LogAppCloseAsync(sessionDuration);

            Log("Lifecycle manager shutdown completed");
        }
        catch (Exception ex)
        {
            Log($"Shutdown error: {ex.Message}");
        }
    }

    public async Task LogErrorAsync(Exception ex, string context)
    {
        try
        {
            await _firebaseService.LogErrorAsync(ex, context, _appVersion);
        }
        catch (Exception logEx)
        {
            Log($"Failed to log error: {logEx.Message}");
        }
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            return await _firebaseService.CheckForUpdatesAsync(_appVersion);
        }
        catch (Exception ex)
        {
            Log($"CheckForUpdatesAsync failed: {ex.Message}");
            return null;
        }
    }

    private static string GetAppVersion()
    {
        try
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    private static void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[HapFirebase] {message}");
    }
}
