using System.Reflection;
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
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            // Prefer informational version (set from <Version> in csproj / -p:Version when building).
            // Release builds use e.g. 1.0.4.HAP so we report that and don't show "update available" for same version.
            var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVersion))
            {
                // Strip optional metadata (e.g. "1.0.4.HAP+abc123" → "1.0.4.HAP")
                var main = infoVersion.Split('+')[0].Trim();
                if (main.Length > 0)
                    return main;
            }

            // Fallback: file version from the exe (ProductVersion)
            var location = asm.Location;
            if (!string.IsNullOrWhiteSpace(location) && System.IO.File.Exists(location))
            {
                var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(location);
                var product = fvi.ProductVersion?.Trim();
                if (!string.IsNullOrWhiteSpace(product))
                    return product;
            }

            var version = asm.GetName().Version;
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
