using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DesktopHub.Infrastructure.Firebase;

namespace DesktopHub.UI.Services;

public class FirebaseLifecycleManager
{
    private readonly IFirebaseService _firebaseService;
    private readonly DateTime _startTime;
    private readonly string _appVersion;

    public FirebaseLifecycleManager(IFirebaseService firebaseService)
    {
        _firebaseService = firebaseService;
        _startTime = DateTime.UtcNow;
        _appVersion = GetAppVersion();
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _firebaseService.InitializeAsync();
            
            if (_firebaseService.IsInitialized)
            {
                await _firebaseService.EnsureLicenseExistsAsync();
                await _firebaseService.RegisterDeviceAsync();
                await _firebaseService.LogAppLaunchAsync(_appVersion);
                _firebaseService.StartHeartbeat();
                
                DebugLogger.Log("Firebase: Lifecycle manager initialized successfully");
            }
            else
            {
                DebugLogger.Log("Firebase: Not initialized, running in offline mode");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Firebase: Initialization error: {ex.Message}");
        }
    }

    public async Task ShutdownAsync()
    {
        try
        {
            _firebaseService.StopHeartbeat();
            
            var sessionDuration = DateTime.UtcNow - _startTime;
            await _firebaseService.LogAppCloseAsync(sessionDuration);
            
            DebugLogger.Log("Firebase: Lifecycle manager shutdown completed");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Firebase: Shutdown error: {ex.Message}");
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
            DebugLogger.Log($"Firebase: Failed to log error: {logEx.Message}");
        }
    }

    public async Task LogUsageEventAsync(string eventType, Dictionary<string, object>? data = null)
    {
        try
        {
            await _firebaseService.LogUsageEventAsync(eventType, data);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Firebase: Failed to log usage event: {ex.Message}");
        }
    }

    public async Task<Infrastructure.Firebase.Models.UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            DebugLogger.Log($"FirebaseLifecycleManager: CheckForUpdatesAsync called with version {_appVersion}");
            var result = await _firebaseService.CheckForUpdatesAsync(_appVersion);
            
            if (result == null)
            {
                DebugLogger.Log("FirebaseLifecycleManager: CheckForUpdatesAsync returned null from FirebaseService");
            }
            else
            {
                DebugLogger.Log($"FirebaseLifecycleManager: CheckForUpdatesAsync returned UpdateInfo - Current={result.CurrentVersion}, Latest={result.LatestVersion}, Available={result.UpdateAvailable}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"FirebaseLifecycleManager: EXCEPTION in CheckForUpdatesAsync: {ex.Message}");
            DebugLogger.Log($"FirebaseLifecycleManager: Stack trace: {ex.StackTrace}");
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
}
