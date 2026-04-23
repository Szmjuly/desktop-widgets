using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using DesktopHub.Core;
using DesktopHub.Infrastructure.Firebase.Models;
using DesktopHub.Infrastructure.Firebase.Utilities;
using DesktopHub.Infrastructure.Logging;

namespace DesktopHub.Infrastructure.Firebase;

public class FirebaseService : IFirebaseService
{
    private const string AppId = "desktophub";
    private const string DefaultDatabaseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com";
    private readonly string _appDataDir;
    private readonly string _configFilePath;
    private readonly FirebaseAuth _auth = new();
    private HttpClient? _httpClient;
    private string? _databaseUrl;
    private DeviceInfo? _deviceInfo;
    private System.Threading.Timer? _heartbeatTimer;
    private DateTime _sessionStartTime;
    private bool _isInitialized;
    private bool _forcedUpdateProcessing;
    private readonly string _username = Environment.UserName.ToLowerInvariant();

    // Every user-scoped / license / device / telemetry path must be prefixed
    // with tenants/{tenantId}/. Raw root-level writes are locked out by rules.
    private string T(string rel) => $"tenants/{_auth.TenantId ?? BuildConfig.TenantId}/{rel}";

    public string TenantPath(string relative) => T(relative);

    // Hashed user id issued by issueToken. Before sign-in completes, fall back
    // to a deterministic placeholder so any early probe fails cleanly rather
    // than leaking the raw Windows username into an RTDB path.
    private string UserKey => _auth.UserId ?? "pending";

    /// <summary>
    /// Exposes the auth object so the developer panel can invoke admin-tier
    /// callable Cloud Functions (pushForceUpdate, clearForceUpdate) with the
    /// current ID token as the Bearer credential.
    /// </summary>
    public FirebaseAuth Auth => _auth;

    /// <summary>Current user's privilege tier: "user" | "dev" | "admin".</summary>
    public string CurrentTier => _auth.Tier ?? "user";

    // Telemetry consent gate. Set to true on startup by App.xaml.cs after
    // reading SettingsService, and flipped any time the user toggles the
    // Privacy switch in Settings. When false, the five methods at the bottom
    // of this file (LogAppLaunch, LogAppClose, LogUsageEvent, LogError,
    // SyncDailyMetrics) short-circuit without writing to Firebase. Heartbeat,
    // license, updates, and role checks are NOT gated -- those are functional,
    // not telemetry.
    private volatile bool _telemetryConsentGiven;

    public void SetTelemetryConsent(bool consent)
    {
        _telemetryConsentGiven = consent;
        InfraLogger.Log($"Firebase: telemetry consent set to {consent}");
    }

    public bool IsTelemetryConsentGiven => _telemetryConsentGiven;

    public event EventHandler<Models.ForcedUpdateInfo>? ForcedUpdateDetected;

    public bool IsInitialized => _isInitialized;

    public FirebaseService()
    {
        _appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopHub"
        );
        _configFilePath = Path.Combine(_appDataDir, "firebase-config.json");
        
        Directory.CreateDirectory(_appDataDir);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            InfraLogger.Log("Firebase: Starting initialization...");

            _deviceInfo = DeviceIdentifier.GetDeviceInfo();
            _databaseUrl = DefaultDatabaseUrl;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _sessionStartTime = DateTime.UtcNow;

            // Read or auto-provision a license key locally so the first-run
            // handshake with the Cloud Function has something to send.
            var licenseKey = await GetLicenseKeyAsync();
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                licenseKey = GenerateFreeLicenseKey();
                await SaveLicenseKeyAsync(licenseKey);
                InfraLogger.Log($"Firebase: Generated FREE license locally: {licenseKey}");
            }

            InfraLogger.Log($"Firebase: Signing in (device={_deviceInfo.DeviceId})...");
            var signedIn = await _auth.SignInAsync(licenseKey, _username, _deviceInfo.DeviceId, BuildConfig.TenantId);
            if (!signedIn)
            {
                InfraLogger.Log("Firebase: SignIn failed -- running in offline mode");
                _isInitialized = false;
                return;
            }

            _isInitialized = true;
            InfraLogger.Log($"Firebase: Initialized; tier={_auth.Tier} device={_deviceInfo.DeviceId}");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Initialization failed: {ex.Message}");
            InfraLogger.Log($"Firebase: Stack trace: {ex.StackTrace}");
            _isInitialized = false;
        }
    }

    public string GetDeviceId()
    {
        return DeviceIdentifier.GetDeviceId();
    }

    public async Task RegisterDeviceAsync()
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return;
        }

        try
        {
            var licenseKey = await GetLicenseKeyAsync();
            var appVersion = GetAppVersion();
            var now = DateTime.UtcNow.ToString("o");

            // Register user and device in new structure
            await RegisterUserAndDeviceAsync(licenseKey, appVersion, now);

            InfraLogger.Log($"Firebase: Device registered - {_deviceInfo.DeviceId}");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to register device: {ex.Message}");
        }
    }

    /// <summary>
    /// Register/update the user and device in the new structured nodes.
    /// users/{username} — canonical user record with linked devices
    /// devices/{device_id} — device record with per-app state
    /// </summary>
    private async Task RegisterUserAndDeviceAsync(string? licenseKey, string appVersion, string timestamp)
    {
        if (_deviceInfo == null) return;

        try
        {
            // Update users/{userKey} — create or update
            await PatchDataAsync(T($"users/{UserKey}"), new Dictionary<string, object>
            {
                ["last_seen"] = timestamp,
                ["user_id"] = _auth.UserId ?? "(pending)"
            });

            // Set first_seen only if not already set (use PUT on the specific field with a rule,
            // but since RTDB doesn't support "write if absent" easily, we just patch —
            // the first call creates it, subsequent calls update last_seen only)
            // Link device to user
            await PutDataAsync(T($"users/{UserKey}/devices/{_deviceInfo.DeviceId}"), true);

            // Update devices/{device_id} — device info stored once
            var deviceData = new Dictionary<string, object>
            {
                ["device_name"] = _deviceInfo.DeviceName,
                ["mac_address"] = _deviceInfo.MacAddress ?? "unknown",
                ["platform"] = "Windows",
                ["platform_version"] = Environment.OSVersion.Version.ToString(),
                ["machine"] = Environment.Is64BitOperatingSystem ? "x64" : "x86",
                ["last_seen"] = timestamp,
                ["status"] = "active",
                ["license_key"] = licenseKey ?? "FREE-AUTO"
            };
            await PatchDataAsync(T($"devices/{_deviceInfo.DeviceId}"), deviceData);

            // Update per-app state on device: devices/{device_id}/apps/{app_id}
            var appState = new Dictionary<string, object>
            {
                ["installed_version"] = appVersion,
                ["last_launch"] = timestamp,
                ["status"] = "active"
            };
            await PatchDataAsync(T($"devices/{_deviceInfo.DeviceId}/apps/{AppId}"), appState);

            InfraLogger.Log($"Firebase: User and device registered in new structure");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: RegisterUserAndDeviceAsync failed (non-fatal): {ex.Message}");
        }
    }

    public async Task UpdateHeartbeatAsync()
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow.ToString("o");
            var appVersion = GetAppVersion();

            // Update devices/{id} with full info (including username) on every heartbeat
            var licenseKey = await GetLicenseKeyAsync();
            await PatchDataAsync(T($"devices/{_deviceInfo.DeviceId}"), new Dictionary<string, object>
            {
                ["device_name"] = _deviceInfo.DeviceName,
                ["mac_address"] = _deviceInfo.MacAddress ?? "unknown",
                ["last_seen"] = now,
                ["status"] = "active",
                ["license_key"] = licenseKey ?? "FREE-AUTO"
            });
            await PatchDataAsync(T($"devices/{_deviceInfo.DeviceId}/apps/{AppId}"), new Dictionary<string, object>
            {
                ["installed_version"] = appVersion,
                ["last_launch"] = now,
                ["status"] = "active"
            });
            // Update user last_seen
            await PatchDataAsync(T($"users/{UserKey}"), new Dictionary<string, object>
            {
                ["last_seen"] = now
            });
            await PutDataAsync(T($"users/{UserKey}/devices/{_deviceInfo.DeviceId}"), true);

            // Check for admin-pushed forced update
            if (!_forcedUpdateProcessing)
            {
                try
                {
                    var forcedUpdate = await CheckForForcedUpdateAsync();
                    if (forcedUpdate != null)
                    {
                        _forcedUpdateProcessing = true;
                        InfraLogger.Log($"Firebase: Forced update detected! Target: v{forcedUpdate.TargetVersion}");
                        ForcedUpdateDetected?.Invoke(this, forcedUpdate);
                    }
                }
                catch (Exception fuEx)
                {
                    InfraLogger.Log($"Firebase: Forced update check failed (non-fatal): {fuEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Heartbeat update failed: {ex.Message}");
        }
    }

    public async Task<LicenseInfo?> GetLicenseInfoAsync()
    {
        if (!_isInitialized || _httpClient == null)
        {
            return null;
        }

        try
        {
            var licenseKey = await GetLicenseKeyAsync();
            if (string.IsNullOrEmpty(licenseKey))
            {
                return null;
            }

            var data = await GetDataAsync<Dictionary<string, object>>(T($"licenses/{AppId}/{licenseKey}"));
            if (data == null)
            {
                return null;
            }

            return new LicenseInfo
            {
                LicenseKey = data.GetValueOrDefault("license_key")?.ToString() ?? licenseKey,
                AppId = data.GetValueOrDefault("app_id")?.ToString() ?? AppId,
                Plan = data.GetValueOrDefault("plan")?.ToString() ?? "free",
                Status = data.GetValueOrDefault("status")?.ToString() ?? "active",
                ExpiresAt = ParseDateTime(data.GetValueOrDefault("expires_at")?.ToString()),
                CreatedAt = ParseDateTime(data.GetValueOrDefault("created_at")?.ToString()) ?? DateTime.UtcNow,
                MaxDevices = ParseInt(data.GetValueOrDefault("max_devices")?.ToString()) ?? -1
            };
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to get license info: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> EnsureLicenseExistsAsync()
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return false;
        }

        try
        {
            var existingKey = await GetLicenseKeyAsync();
            if (!string.IsNullOrEmpty(existingKey))
            {
                return true;
            }

            var licenseKey = GenerateFreeLicenseKey();
            var licenseData = new Dictionary<string, object>
            {
                ["license_key"] = licenseKey,
                ["app_id"] = AppId,
                ["plan"] = "free",
                ["tier"] = "free",
                ["status"] = "active",
                ["source"] = "auto-created",
                ["created_at"] = DateTime.UtcNow.ToString("o"),
                ["expires_at"] = null!,
                ["max_devices"] = -1,
                ["documents_limit"] = 0,
                ["documents_used"] = 0,
                ["is_bundle"] = false,
                ["email"] = null!,
                ["user_id"] = _auth.UserId ?? "(pending)"
            };

            await PutDataAsync(T($"licenses/{AppId}/{licenseKey}"), licenseData);

            await SaveLicenseKeyAsync(licenseKey);

            InfraLogger.Log($"Firebase: Created free license - {licenseKey}");
            return true;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to create license: {ex.Message}");
            return false;
        }
    }

    private static string GetEventMonth() => DateTime.UtcNow.ToString("yyyy-MM");

    public async Task LogAppLaunchAsync(string appVersion)
    {
        if (!_telemetryConsentGiven) return;
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow.ToString("o");

            // --- New structure: events/{app_id}/{YYYY-MM}/ ---
            var eventData = new Dictionary<string, object>
            {
                ["event_type"] = "app_launch",
                ["device_id"] = _deviceInfo.DeviceId,
                ["user_id"] = _auth.UserId ?? "(pending)",
                ["timestamp"] = now,
                ["app_version"] = appVersion
            };
            await PostDataAsync(T($"events/{AppId}/{GetEventMonth()}"), eventData);

            InfraLogger.Log($"Firebase: App launch logged");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to log app launch: {ex.Message}");
        }
    }

    public async Task LogAppCloseAsync(TimeSpan sessionDuration)
    {
        if (!_telemetryConsentGiven) return;
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow.ToString("o");
            var appVersion = GetAppVersion();

            // --- New structure: events/ + mark device inactive ---
            var eventData = new Dictionary<string, object>
            {
                ["event_type"] = "app_close",
                ["device_id"] = _deviceInfo.DeviceId,
                ["user_id"] = _auth.UserId ?? "(pending)",
                ["timestamp"] = now,
                ["app_version"] = appVersion,
                ["session_duration_seconds"] = (int)sessionDuration.TotalSeconds
            };
            await PostDataAsync(T($"events/{AppId}/{GetEventMonth()}"), eventData);

            // Mark app as inactive on device
            await PatchDataAsync(T($"devices/{_deviceInfo.DeviceId}/apps/{AppId}"), new Dictionary<string, object>
            {
                ["status"] = "inactive",
                ["last_seen"] = now
            });

            InfraLogger.Log($"Firebase: App close logged (session: {sessionDuration.TotalMinutes:F1} min)");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to log app close: {ex.Message}");
        }
    }

    public async Task LogUsageEventAsync(string eventType, Dictionary<string, object>? data = null)
    {
        if (!_telemetryConsentGiven) return;
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow.ToString("o");

            // --- New structure: events/{app_id}/{YYYY-MM}/ ---
            var eventData = new Dictionary<string, object>
            {
                ["event_type"] = eventType,
                ["device_id"] = _deviceInfo.DeviceId,
                ["user_id"] = _auth.UserId ?? "(pending)",
                ["timestamp"] = now,
                ["app_version"] = GetAppVersion()
            };
            if (data != null)
            {
                foreach (var kvp in data)
                    eventData[kvp.Key] = kvp.Value;
            }
            await PostDataAsync(T($"events/{AppId}/{GetEventMonth()}"), eventData);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to log usage event: {ex.Message}");
        }
    }

    public async Task LogErrorAsync(Exception ex, string context, string appVersion)
    {
        if (!_telemetryConsentGiven) return;
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return;
        }

        try
        {
            var errorData = new Dictionary<string, object>
            {
                ["device_id"] = _deviceInfo.DeviceId,
                ["user_id"] = _auth.UserId ?? "(pending)",
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["error_type"] = ex.GetType().Name,
                ["error_message"] = ex.Message,
                ["stack_trace"] = ex.StackTrace ?? "",
                ["context"] = context,
                ["app_version"] = appVersion
            };

            await PostDataAsync(T($"errors/{AppId}/{GetEventMonth()}"), errorData);

            InfraLogger.Log($"Firebase: Error logged - {ex.GetType().Name}");
        }
        catch (Exception logEx)
        {
            InfraLogger.Log($"Firebase: Failed to log error: {logEx.Message}");
        }
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(string currentVersion)
    {
        InfraLogger.Log($"FirebaseService: CheckForUpdatesAsync START - currentVersion={currentVersion}");
        InfraLogger.Log($"FirebaseService: _isInitialized={_isInitialized}, _httpClient={(_httpClient != null)}, _deviceInfo={(_deviceInfo != null)}");
        
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            InfraLogger.Log("FirebaseService: CheckForUpdatesAsync - Early return due to uninitialized state");
            return null;
        }

        try
        {
            InfraLogger.Log($"FirebaseService: Fetching version data from app_versions/{AppId}");
            var data = await GetDataAsync<Dictionary<string, object>>($"app_versions/{AppId}");
            
            if (data == null)
            {
                InfraLogger.Log("FirebaseService: GetDataAsync returned null - no version data found");
                return null;
            }

            InfraLogger.Log($"FirebaseService: Received {data.Count} fields from Firebase");
            foreach (var kvp in data)
            {
                InfraLogger.Log($"FirebaseService:   {kvp.Key} = {kvp.Value}");
            }

            var latestVersion = data.GetValueOrDefault("latest_version")?.ToString();
            InfraLogger.Log($"FirebaseService: latest_version from Firebase: {latestVersion}");
            
            if (string.IsNullOrEmpty(latestVersion))
            {
                InfraLogger.Log("FirebaseService: latest_version is null or empty - returning null");
                return null;
            }

            var updateInfo = new UpdateInfo
            {
                LatestVersion = latestVersion,
                CurrentVersion = currentVersion,
                ReleaseNotes = data.GetValueOrDefault("release_notes")?.ToString(),
                DownloadUrl = data.GetValueOrDefault("download_url")?.ToString(),
                ReleaseDate = ParseDateTime(data.GetValueOrDefault("release_date")?.ToString()),
                RequiredUpdate = ParseBool(data.GetValueOrDefault("required_update")?.ToString()) ?? false
            };

            InfraLogger.Log($"FirebaseService: Created UpdateInfo:");
            InfraLogger.Log($"FirebaseService:   LatestVersion={updateInfo.LatestVersion}");
            InfraLogger.Log($"FirebaseService:   CurrentVersion={updateInfo.CurrentVersion}");
            InfraLogger.Log($"FirebaseService:   UpdateAvailable={updateInfo.UpdateAvailable}");
            InfraLogger.Log($"FirebaseService:   DownloadUrl={updateInfo.DownloadUrl}");
            InfraLogger.Log($"FirebaseService:   ReleaseNotes={updateInfo.ReleaseNotes}");

            var now = DateTime.UtcNow.ToString("o");
            var checkData = new Dictionary<string, object>
            {
                ["event_type"] = "update_check",
                ["device_id"] = _deviceInfo.DeviceId,
                ["user_id"] = _auth.UserId ?? "(pending)",
                ["current_version"] = currentVersion,
                ["latest_version"] = latestVersion,
                ["update_available"] = updateInfo.UpdateAvailable,
                ["timestamp"] = now
            };

            InfraLogger.Log("FirebaseService: Logging update check to Firebase");
            await PostDataAsync(T($"events/{AppId}/{GetEventMonth()}"), checkData);

            // Also update the device record with last update check info
            await PatchDataAsync(T($"devices/{_deviceInfo.DeviceId}/apps/{AppId}"), new Dictionary<string, object>
            {
                ["last_update_check"] = now,
                ["last_known_version"] = latestVersion
            });

            InfraLogger.Log("FirebaseService: CheckForUpdatesAsync SUCCESS - returning UpdateInfo");
            return updateInfo;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"FirebaseService: EXCEPTION in CheckForUpdatesAsync: {ex.Message}");
            InfraLogger.Log($"FirebaseService: Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    public async Task<bool> GetFeatureFlagAsync(string flagName, bool defaultValue = false)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return defaultValue;
        }

        try
        {
            // Check device-specific flag first: feature_flags/{deviceId}/{flagName}
            var deviceFlag = await GetDataAsync<object>($"feature_flags/{_deviceInfo.DeviceId}/{flagName}");
            if (deviceFlag != null)
            {
                return ParseBool(deviceFlag.ToString()) ?? defaultValue;
            }

            // Fall back to license-level flag: feature_flags/licenses/{licenseKey}/{flagName}
            var licenseKey = await GetLicenseKeyAsync();
            if (!string.IsNullOrEmpty(licenseKey))
            {
                var licenseFlag = await GetDataAsync<object>($"feature_flags/licenses/{licenseKey}/{flagName}");
                if (licenseFlag != null)
                {
                    return ParseBool(licenseFlag.ToString()) ?? defaultValue;
                }
            }

            // Fall back to global flag: feature_flags/global/{flagName}
            var globalFlag = await GetDataAsync<object>($"feature_flags/global/{flagName}");
            if (globalFlag != null)
            {
                return ParseBool(globalFlag.ToString()) ?? defaultValue;
            }

            return defaultValue;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to get feature flag '{flagName}': {ex.Message}");
            return defaultValue;
        }
    }

    public async Task<bool> IsMetricsViewerEnabledAsync()
    {
        return await GetFeatureFlagAsync("metrics_viewer_enabled", defaultValue: true);
    }

    public Task<bool> IsUserAdminAsync(string? windowsUsername = null)
    {
        // Role comes from the JWT claim minted by Cloud Functions. No RTDB read.
        var tier = _auth.Tier;
        return Task.FromResult(tier == "admin" || tier == "dev");
    }

    public Task<bool> IsUserDevAsync(string? windowsUsername = null)
    {
        return Task.FromResult(_auth.Tier == "dev");
    }

    public async Task<bool> IsCheatSheetEditorAsync(string? windowsUsername = null)
    {
        if (!_isInitialized || _httpClient == null)
            return false;

        try
        {
            // Admin / dev tier always has editor privileges — claim-based, no RTDB read.
            var tier = _auth.Tier;
            if (tier == "admin" || tier == "dev")
            {
                InfraLogger.Log($"Firebase: CheatSheet editor check = true (tier={tier})");
                return true;
            }

            // Tenant-scoped editor list keyed by hashed user id.
            var editorFlag = await GetDataAsync<object>(T($"cheat_sheet_editors/{UserKey}"));
            if (editorFlag == null) return false;
            var s = editorFlag.ToString()?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(s)) return false;
            if (s == "false" || s == "0" || s == "null" || s == "no" || s == "off") return false;
            InfraLogger.Log($"Firebase: CheatSheet editor check = true (editor role)");
            return true;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: CheatSheet editor check failed: {ex.Message}");
            return false;
        }
    }

    public async Task<Dictionary<string, object>?> GetNodeAsync(string path)
    {
        if (!_isInitialized || _httpClient == null || string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return await GetDataAsync<Dictionary<string, object>>(path);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: GetNodeAsync failed for '{path}': {ex.Message}");
            return null;
        }
    }

    public async Task<bool> SetNodeAsync(string path, object data)
    {
        if (!_isInitialized || _httpClient == null || string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            await PutDataAsync(path, data);
            return true;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: SetNodeAsync failed for '{path}': {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteNodeAsync(string path)
    {
        if (!_isInitialized || _httpClient == null || string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            await DeleteDataAsync(path);
            return true;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: DeleteNodeAsync failed for '{path}': {ex.Message}");
            return false;
        }
    }

    public async Task LogUpdateInstalledAsync(string version)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow.ToString("o");
            var installData = new Dictionary<string, object>
            {
                ["event_type"] = "update_installed",
                ["device_id"] = _deviceInfo.DeviceId,
                ["user_id"] = _auth.UserId ?? "(pending)",
                ["version"] = version,
                ["timestamp"] = now
            };

            await PostDataAsync(T($"events/{AppId}/{GetEventMonth()}"), installData);

            // Update installed version on device
            await PatchDataAsync(T($"devices/{_deviceInfo?.DeviceId}/apps/{AppId}"), new Dictionary<string, object>
            {
                ["installed_version"] = version
            });

            InfraLogger.Log($"Firebase: Update installation logged - v{version}");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to log update installation: {ex.Message}");
        }
    }

    public void StartHeartbeat()
    {
        if (!_isInitialized)
        {
            return;
        }

        _heartbeatTimer = new System.Threading.Timer(
            async _ => await UpdateHeartbeatAsync(),
            null,
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(10)
        );

        InfraLogger.Log("Firebase: Heartbeat started (every 10 minutes)");
    }

    public void StopHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;

        InfraLogger.Log("Firebase: Heartbeat stopped");
    }

    // --- Forced update ---

    public async Task<ForcedUpdateInfo?> CheckForForcedUpdateAsync()
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
            return null;

        try
        {
            var data = await GetDataAsync<Dictionary<string, object>>($"force_update/{_deviceInfo.DeviceId}");
            if (data == null)
                return null;

            // Filter by app_id — only process entries for this app (or legacy entries with no app_id)
            var entryAppId = data.GetValueOrDefault("app_id")?.ToString();
            if (!string.IsNullOrEmpty(entryAppId) && entryAppId != AppId)
                return null;

            var status = data.GetValueOrDefault("status")?.ToString();
            if (status != "pending")
                return null;

            var targetVersion = data.GetValueOrDefault("target_version")?.ToString();
            var downloadUrl = data.GetValueOrDefault("download_url")?.ToString();
            if (string.IsNullOrEmpty(targetVersion) || string.IsNullOrEmpty(downloadUrl))
                return null;

            // Only act if target version is newer than current
            var currentVersion = GetAppVersion();
            if (Version.TryParse(targetVersion, out var target) &&
                Version.TryParse(currentVersion, out var current) &&
                target <= current)
            {
                // Already up to date — mark completed and clean up
                await UpdateForcedUpdateStatusAsync("completed");
                return null;
            }

            var retryCount = 0;
            if (data.TryGetValue("retry_count", out var rc))
                int.TryParse(rc?.ToString(), out retryCount);

            return new ForcedUpdateInfo
            {
                TargetVersion = targetVersion,
                DownloadUrl = downloadUrl,
                PushedBy = data.GetValueOrDefault("pushed_by")?.ToString(),
                PushedAt = ParseDateTime(data.GetValueOrDefault("pushed_at")?.ToString()),
                Status = status,
                RetryCount = retryCount,
                Error = data.GetValueOrDefault("error")?.ToString()
            };
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: CheckForForcedUpdateAsync failed: {ex.Message}");
            return null;
        }
    }

    public async Task UpdateForcedUpdateStatusAsync(string status, string? error = null)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
            return;

        try
        {
            var update = new Dictionary<string, object>
            {
                ["status"] = status,
                ["status_updated_at"] = DateTime.UtcNow.ToString("o")
            };

            if (!string.IsNullOrEmpty(error))
                update["error"] = error;

            // Increment retry_count on failure
            if (status == "failed")
            {
                var data = await GetDataAsync<Dictionary<string, object>>($"force_update/{_deviceInfo.DeviceId}");
                var retryCount = 0;
                if (data?.TryGetValue("retry_count", out var rc) == true)
                    int.TryParse(rc?.ToString(), out retryCount);
                update["retry_count"] = retryCount + 1;

                // If max retries (3) exceeded, keep as failed; otherwise revert to pending for retry
                if (retryCount + 1 < 3)
                {
                    update["status"] = "pending";
                    InfraLogger.Log($"Firebase: Forced update retry {retryCount + 1}/3 — will retry on next heartbeat");
                }
                else
                {
                    InfraLogger.Log($"Firebase: Forced update failed after 3 retries — marking as failed");
                }
            }

            await PatchDataAsync($"force_update/{_deviceInfo.DeviceId}", update);
            InfraLogger.Log($"Firebase: Forced update status → {update["status"]}");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: UpdateForcedUpdateStatusAsync failed: {ex.Message}");
        }
    }

    public async Task CompleteForcedUpdateIfPendingAsync()
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
            return;

        try
        {
            var data = await GetDataAsync<Dictionary<string, object>>($"force_update/{_deviceInfo.DeviceId}");
            if (data == null)
                return;

            var status = data.GetValueOrDefault("status")?.ToString();
            // If we're launching and the entry says "installing", the update succeeded
            if (status == "installing" || status == "downloading")
            {
                await PatchDataAsync($"force_update/{_deviceInfo.DeviceId}", new Dictionary<string, object>
                {
                    ["status"] = "completed",
                    ["status_updated_at"] = DateTime.UtcNow.ToString("o")
                });
                InfraLogger.Log("Firebase: Forced update marked as completed (post-restart)");
                _forcedUpdateProcessing = false;
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: CompleteForcedUpdateIfPendingAsync failed: {ex.Message}");
        }
    }

    private string GenerateFreeLicenseKey()
    {
        var deviceId = _deviceInfo?.DeviceId ?? Guid.NewGuid().ToString();
        var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(deviceId));
        var hashStr = Convert.ToHexString(hash)[..8];

        var random = new Random();
        var suffix = new string(Enumerable.Range(0, 8)
            .Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[random.Next(36)])
            .ToArray());

        return $"FREE-{hashStr}-{suffix}";
    }

    private async Task<string?> GetLicenseKeyAsync()
    {
        var licenseFile = Path.Combine(_appDataDir, "license_key.txt");
        if (File.Exists(licenseFile))
        {
            return (await File.ReadAllTextAsync(licenseFile)).Trim();
        }
        return null;
    }

    private async Task SaveLicenseKeyAsync(string licenseKey)
    {
        var licenseFile = Path.Combine(_appDataDir, "license_key.txt");
        await File.WriteAllTextAsync(licenseFile, licenseKey);
    }

    private string GetAppVersion()
    {
        return System.Reflection.Assembly.GetEntryAssembly()?
            .GetName().Version?.ToString() ?? "1.0.0";
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        return DateTime.TryParse(value, out var result) ? result : null;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        return int.TryParse(value, out var result) ? result : null;
    }

    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        return bool.TryParse(value, out var result) ? result : null;
    }

    // ─────────────────── REST helpers ───────────────────
    //
    // All RTDB REST calls now authenticate with a short-lived Firebase ID token
    // (minted by the issueToken Cloud Function and exchanged for an ID token via
    // Identity Toolkit). The `?auth=<idToken>` parameter runs through database
    // rules, so every call is scoped by the caller's tier claim.

    private async Task<string?> BuildUrlAsync(string path)
    {
        if (_httpClient == null || string.IsNullOrEmpty(_databaseUrl)) return null;
        var idToken = await _auth.GetIdTokenAsync();
        if (string.IsNullOrEmpty(idToken))
        {
            InfraLogger.Log($"Firebase: no ID token available, skipping call to {path}");
            return null;
        }
        return $"{_databaseUrl}/{path}.json?auth={Uri.EscapeDataString(idToken)}";
    }

    private async Task PutDataAsync(string path, object data)
    {
        var url = await BuildUrlAsync(path);
        if (url == null) return;
        var json = JsonConvert.SerializeObject(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient!.PutAsync(url, content);
    }

    private async Task PostDataAsync(string path, object data)
    {
        var url = await BuildUrlAsync(path);
        if (url == null) return;
        var json = JsonConvert.SerializeObject(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient!.PostAsync(url, content);
    }

    private async Task<T?> GetDataAsync<T>(string path)
    {
        var url = await BuildUrlAsync(path);
        if (url == null) return default;
        var response = await _httpClient!.GetAsync(url);
        if (!response.IsSuccessStatusCode) return default;

        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(json) || json == "null") return default;
        return JsonConvert.DeserializeObject<T>(json);
    }

    private async Task PatchDataAsync(string path, object data)
    {
        var url = await BuildUrlAsync(path);
        if (url == null) return;
        var json = JsonConvert.SerializeObject(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
        await _httpClient!.SendAsync(request);
    }

    // --- Metrics sync for admin multi-user view ---

    public async Task SyncDailyMetricsAsync(string date, Dictionary<string, object> data)
    {
        if (!_telemetryConsentGiven) return;
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return;

        try
        {
            await PutDataAsync(T($"metrics/{_deviceInfo.DeviceId}/{date}"), data);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to sync daily metrics: {ex.Message}");
        }
    }

    public async Task<Dictionary<string, Dictionary<string, Dictionary<string, object>>>?> GetAllDeviceMetricsAsync()
    {
        if (!_isInitialized || _httpClient == null) return null;

        try
        {
            return await GetDataAsync<Dictionary<string, Dictionary<string, Dictionary<string, object>>>>(T("metrics"));
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to read all device metrics: {ex.Message}");
            return null;
        }
    }

    public async Task<Dictionary<string, Dictionary<string, object>>?> GetDevicesAsync()
    {
        if (!_isInitialized || _httpClient == null) return null;

        try
        {
            return await GetDataAsync<Dictionary<string, Dictionary<string, object>>>(T("devices"));
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to read devices: {ex.Message}");
            return null;
        }
    }

    // --- Retention policy ---

    /// <summary>
    /// Reads the retention policy from Firebase (config/retention_policy/events_months)
    /// and deletes event/error month nodes older than the configured period.
    /// Default: 3 months if no config is set.
    /// Admin can change the value in Firebase at any time and all clients will follow.
    /// </summary>
    public async Task EnforceRetentionPolicyAsync()
    {
        if (!_isInitialized || _httpClient == null) return;

        try
        {
            // Read admin-configurable retention period from Firebase
            var retentionMonths = 3; // Default
            var configVal = await GetDataAsync<object>("config/retention_policy/events_months");
            if (configVal != null && int.TryParse(configVal.ToString(), out var parsed) && parsed > 0)
            {
                retentionMonths = parsed;
            }

            InfraLogger.Log($"Firebase: Retention policy = {retentionMonths} months");

            var cutoff = DateTime.UtcNow.AddMonths(-retentionMonths);
            var cutoffMonth = cutoff.ToString("yyyy-MM");

            // Clean up events/ months
            await CleanupOldMonthNodes(T($"events/{AppId}"), cutoffMonth);

            // Clean up errors/ months
            await CleanupOldMonthNodes(T($"errors/{AppId}"), cutoffMonth);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Retention policy enforcement failed: {ex.Message}");
        }
    }

    private async Task CleanupOldMonthNodes(string basePath, string cutoffMonth)
    {
        try
        {
            var allMonths = await GetDataAsync<Dictionary<string, object>>(basePath);
            if (allMonths == null) return;

            var deletedCount = 0;
            foreach (var monthKey in allMonths.Keys)
            {
                // Month keys are "YYYY-MM" format — simple string comparison works
                if (string.Compare(monthKey, cutoffMonth, StringComparison.Ordinal) < 0)
                {
                    await DeleteDataAsync($"{basePath}/{monthKey}");
                    deletedCount++;
                    InfraLogger.Log($"Firebase: Deleted expired month node {basePath}/{monthKey}");
                }
            }

            if (deletedCount > 0)
                InfraLogger.Log($"Firebase: Cleaned up {deletedCount} expired month(s) from {basePath}");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Cleanup failed for {basePath}: {ex.Message}");
        }
    }

    private async Task DeleteDataAsync(string path)
    {
        var url = await BuildUrlAsync(path);
        if (url == null) return;
        await _httpClient!.DeleteAsync(url);
    }

}

