using DesktopHub.Infrastructure.Firebase.Models;

namespace DesktopHub.Infrastructure.Firebase;

public interface IFirebaseService
{
    Task InitializeAsync();
    bool IsInitialized { get; }
    
    string GetDeviceId();
    Task RegisterDeviceAsync();
    Task UpdateHeartbeatAsync();
    Task<LicenseInfo?> GetLicenseInfoAsync();
    Task<bool> EnsureLicenseExistsAsync();
    Task LogAppLaunchAsync(string appVersion);
    Task LogAppCloseAsync(TimeSpan sessionDuration);
    Task LogUsageEventAsync(string eventType, Dictionary<string, object>? data = null);
    Task LogErrorAsync(Exception ex, string context, string appVersion);
    Task<UpdateInfo?> CheckForUpdatesAsync(string currentVersion);
    Task LogUpdateInstalledAsync(string version);
    void StartHeartbeat();
    void StopHeartbeat();
    
    // Feature flags
    Task<bool> GetFeatureFlagAsync(string flagName, bool defaultValue = false);
    Task<bool> IsMetricsViewerEnabledAsync();

    // Admin management — checks Firebase for Windows username-based admin privileges
    Task<bool> IsUserAdminAsync(string? windowsUsername = null);

    // Metrics sync — dedicated metrics/ node for admin multi-user view
    Task SyncDailyMetricsAsync(string date, Dictionary<string, object> data);
    Task<Dictionary<string, Dictionary<string, Dictionary<string, object>>>?> GetAllDeviceMetricsAsync();
    Task<Dictionary<string, Dictionary<string, object>>?> GetDevicesAsync();
}
