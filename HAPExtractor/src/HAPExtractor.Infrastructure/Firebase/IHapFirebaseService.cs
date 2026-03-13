using HAPExtractor.Infrastructure.Firebase.Models;

namespace HAPExtractor.Infrastructure.Firebase;

public interface IHapFirebaseService
{
    Task InitializeAsync();
    bool IsInitialized { get; }

    string GetDeviceId();
    Task RegisterDeviceAsync();
    Task UpdateHeartbeatAsync();
    Task LogAppLaunchAsync(string appVersion);
    Task LogAppCloseAsync(TimeSpan sessionDuration);
    Task LogErrorAsync(Exception ex, string context, string appVersion);
    Task<UpdateInfo?> CheckForUpdatesAsync(string currentVersion);
    void StartHeartbeat();
    void StopHeartbeat();

    // Forced update — admin can push updates to specific devices
    Task<ForcedUpdateInfo?> CheckForForcedUpdateAsync();
    Task UpdateForcedUpdateStatusAsync(string status, string? error = null);
    Task CompleteForcedUpdateIfPendingAsync();

    // Forced update event — raised when heartbeat detects a pending forced update
    event EventHandler<ForcedUpdateInfo>? ForcedUpdateDetected;
}
