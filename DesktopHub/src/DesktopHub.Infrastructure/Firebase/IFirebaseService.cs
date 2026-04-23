using DesktopHub.Infrastructure.Firebase.Models;

namespace DesktopHub.Infrastructure.Firebase;

public interface IFirebaseService
{
    Task InitializeAsync();
    bool IsInitialized { get; }

    /// <summary>
    /// The auth manager, exposed so callers can invoke admin-tier Cloud
    /// Functions with the current ID token as the Bearer credential.
    /// </summary>
    FirebaseAuth Auth { get; }

    /// <summary>Current caller's privilege tier: "user" | "dev" | "admin".</summary>
    string CurrentTier { get; }

    /// <summary>
    /// Prefixes a relative path with the current tenant namespace:
    /// <c>TenantPath("devices")</c> -> <c>tenants/internal/devices</c>.
    /// Use for any RTDB path that lives under the tenant subtree.
    /// </summary>
    string TenantPath(string relative);

    /// <summary>
    /// Whether the user has consented to telemetry. The five telemetry methods
    /// (LogAppLaunch, LogAppClose, LogUsageEvent, LogError, SyncDailyMetrics)
    /// short-circuit when this is false. Functional paths (heartbeat, license,
    /// updates, roles) are not gated.
    /// </summary>
    bool IsTelemetryConsentGiven { get; }

    /// <summary>
    /// Apply the user's telemetry preference. Called on startup from App.xaml.cs
    /// after reading SettingsService, and any time the Settings toggle changes.
    /// </summary>
    void SetTelemetryConsent(bool consent);

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

    // DEV role — highest internal privilege for developer tooling
    Task<bool> IsUserDevAsync(string? windowsUsername = null);

    // Cheat sheet editor role — independent from admin, edit access = admin OR editor
    Task<bool> IsCheatSheetEditorAsync(string? windowsUsername = null);

    // Metrics sync — dedicated metrics/ node for admin multi-user view
    Task SyncDailyMetricsAsync(string date, Dictionary<string, object> data);
    Task<Dictionary<string, Dictionary<string, Dictionary<string, object>>>?> GetAllDeviceMetricsAsync();
    Task<Dictionary<string, Dictionary<string, object>>?> GetDevicesAsync();

    // Forced update — admin can push updates to specific devices
    Task<ForcedUpdateInfo?> CheckForForcedUpdateAsync();
    Task UpdateForcedUpdateStatusAsync(string status, string? error = null);
    Task CompleteForcedUpdateIfPendingAsync();

    // Forced update event — raised when heartbeat detects a pending forced update
    event EventHandler<ForcedUpdateInfo>? ForcedUpdateDetected;

    // Developer panel helpers — direct node operations for DEV tooling only.
    Task<Dictionary<string, object>?> GetNodeAsync(string path);
    Task<bool> SetNodeAsync(string path, object data);
    Task<bool> DeleteNodeAsync(string path);
}
