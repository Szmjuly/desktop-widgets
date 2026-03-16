using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
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
    private FirebaseApp? _firebaseApp;
    private HttpClient? _httpClient;
    private string? _databaseUrl;
    private DeviceInfo? _deviceInfo;
    private System.Threading.Timer? _heartbeatTimer;
    private DateTime _sessionStartTime;
    private bool _isInitialized;
    private bool _forcedUpdateProcessing;
    private readonly string _username = Environment.UserName.ToLowerInvariant();

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
            
            InfraLogger.Log("Firebase: Loading credentials...");
            var credential = await GetCredentialAsync();
            if (credential == null)
            {
                InfraLogger.Log("Firebase: No credentials found, running in offline mode");
                _isInitialized = false;
                return;
            }

            InfraLogger.Log("Firebase: Getting database URL...");
            var databaseUrl = await GetDatabaseUrlAsync();
            if (string.IsNullOrEmpty(databaseUrl))
            {
                InfraLogger.Log("Firebase: No database URL found");
                _isInitialized = false;
                return;
            }

            InfraLogger.Log("Firebase: Creating Firebase app instance...");
            if (FirebaseApp.DefaultInstance == null)
            {
                _firebaseApp = FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ServiceAccountId = "firebase-adminsdk-ftaw@licenses-ff136.iam.gserviceaccount.com"
                });
            }
            else
            {
                _firebaseApp = FirebaseApp.DefaultInstance;
            }

            InfraLogger.Log("Firebase: Setting up HTTP client and device info...");
            _databaseUrl = databaseUrl;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _deviceInfo = DeviceIdentifier.GetDeviceInfo();
            _sessionStartTime = DateTime.UtcNow;
            _isInitialized = true;

            InfraLogger.Log($"Firebase: Initialized successfully for device {_deviceInfo.DeviceId}");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Initialization failed: {ex.Message}");
            InfraLogger.Log($"Firebase: Stack trace: {ex.StackTrace}");
            _isInitialized = false;
        }
    }

    private async Task<GoogleCredential?> GetCredentialAsync()
    {
        if (File.Exists(_configFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                return GoogleCredential.FromJson(json);
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"Firebase: Failed to load config from file: {ex.Message}");
            }
        }

        var embeddedConfig = GetEmbeddedServiceAccount();
        if (embeddedConfig != null)
        {
            try
            {
                return GoogleCredential.FromJson(embeddedConfig);
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"Firebase: Failed to load embedded config: {ex.Message}");
            }
        }

        return null;
    }

    private async Task<string?> GetDatabaseUrlAsync()
    {
        const string defaultUrl = "https://licenses-ff136-default-rtdb.firebaseio.com";
        
        if (File.Exists(_configFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (config?.ContainsKey("databaseURL") == true)
                {
                    return config["databaseURL"]?.ToString();
                }
            }
            catch
            {
                // Ignore
            }
        }

        return defaultUrl;
    }

    private string? GetEmbeddedServiceAccount()
    {
        try
        {
            // First try to read from embedded resource (for Release single-file builds)
            InfraLogger.Log("Firebase: Checking for embedded resource credentials...");
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                var resourceName = "firebase-license.json";
                var resourceNames = assembly.GetManifestResourceNames();
                var matchingResource = resourceNames.FirstOrDefault(r => r.EndsWith(resourceName));
                
                if (matchingResource != null)
                {
                    InfraLogger.Log($"Firebase: Found embedded resource: {matchingResource}");
                    using (var stream = assembly.GetManifestResourceStream(matchingResource))
                    {
                        if (stream != null)
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                var content = reader.ReadToEnd();
                                InfraLogger.Log("Firebase: Successfully read credentials from embedded resource");
                                return content;
                            }
                        }
                    }
                }
                else
                {
                    InfraLogger.Log($"Firebase: No embedded resource found. Available resources: {string.Join(", ", resourceNames)}");
                }
            }
            
            // Fallback: Check the application directory (for non-single-file builds)
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var appDirLicense = Path.Combine(appDir, "firebase-license.json");
            
            InfraLogger.Log($"Firebase: Checking for credentials at: {appDirLicense}");
            if (File.Exists(appDirLicense))
            {
                InfraLogger.Log("Firebase: Found credentials in application directory");
                return File.ReadAllText(appDirLicense);
            }
            
            // Fallback: Look for firebase-license.json in the secrets/ folder (for Debug builds)
            var solutionRoot = FindSolutionRoot(appDir);
            if (solutionRoot != null)
            {
                var licenseFile = Path.Combine(solutionRoot, "secrets", "firebase-license.json");
                InfraLogger.Log($"Firebase: Checking for credentials at: {licenseFile}");
                if (File.Exists(licenseFile))
                {
                    InfraLogger.Log("Firebase: Found credentials in secrets folder");
                    return File.ReadAllText(licenseFile);
                }
            }
            
            InfraLogger.Log("Firebase: No credentials file found in any location");
            return null;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to read embedded service account: {ex.Message}");
            InfraLogger.Log($"Firebase: Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    private string? FindSolutionRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            // Look for .sln file or specific marker
            if (dir.GetFiles("*.sln").Any() || dir.GetFiles("firebase-license.json").Any())
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
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

            InfraLogger.Log($"Firebase: Device registered - {_deviceInfo.DeviceId} (user: {_username})");
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
            // Update users/{username} — create or update
            await PatchDataAsync($"users/{_username}", new Dictionary<string, object>
            {
                ["last_seen"] = timestamp,
                ["display_name"] = Environment.UserName // preserve original casing
            });

            // Set first_seen only if not already set (use PUT on the specific field with a rule,
            // but since RTDB doesn't support "write if absent" easily, we just patch —
            // the first call creates it, subsequent calls update last_seen only)
            // Link device to user
            await PutDataAsync($"users/{_username}/devices/{_deviceInfo.DeviceId}", true);

            // Update devices/{device_id} — device info stored once
            var deviceData = new Dictionary<string, object>
            {
                ["device_name"] = _deviceInfo.DeviceName,
                ["username"] = _username,
                ["mac_address"] = _deviceInfo.MacAddress ?? "unknown",
                ["platform"] = "Windows",
                ["platform_version"] = Environment.OSVersion.Version.ToString(),
                ["machine"] = Environment.Is64BitOperatingSystem ? "x64" : "x86",
                ["last_seen"] = timestamp,
                ["status"] = "active",
                ["license_key"] = licenseKey ?? "FREE-AUTO"
            };
            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}", deviceData);

            // Update per-app state on device: devices/{device_id}/apps/{app_id}
            var appState = new Dictionary<string, object>
            {
                ["installed_version"] = appVersion,
                ["last_launch"] = timestamp,
                ["status"] = "active"
            };
            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}/apps/{AppId}", appState);

            InfraLogger.Log($"Firebase: User '{_username}' and device registered in new structure");
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
            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}", new Dictionary<string, object>
            {
                ["device_name"] = _deviceInfo.DeviceName,
                ["username"] = _username,
                ["mac_address"] = _deviceInfo.MacAddress ?? "unknown",
                ["last_seen"] = now,
                ["status"] = "active",
                ["license_key"] = licenseKey ?? "FREE-AUTO"
            });
            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}/apps/{AppId}", new Dictionary<string, object>
            {
                ["installed_version"] = appVersion,
                ["last_launch"] = now,
                ["status"] = "active"
            });
            // Update user last_seen
            await PatchDataAsync($"users/{_username}", new Dictionary<string, object>
            {
                ["last_seen"] = now
            });
            await PutDataAsync($"users/{_username}/devices/{_deviceInfo.DeviceId}", true);

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

            var data = await GetDataAsync<Dictionary<string, object>>($"licenses/{licenseKey}");
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
                ["username"] = _username
            };

            await PutDataAsync($"licenses/{licenseKey}", licenseData);

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
                ["username"] = _username,
                ["timestamp"] = now,
                ["app_version"] = appVersion
            };
            await PostDataAsync($"events/{AppId}/{GetEventMonth()}", eventData);

            InfraLogger.Log($"Firebase: App launch logged (user: {_username})");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to log app launch: {ex.Message}");
        }
    }

    public async Task LogAppCloseAsync(TimeSpan sessionDuration)
    {
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
                ["username"] = _username,
                ["timestamp"] = now,
                ["app_version"] = appVersion,
                ["session_duration_seconds"] = (int)sessionDuration.TotalSeconds
            };
            await PostDataAsync($"events/{AppId}/{GetEventMonth()}", eventData);

            // Mark app as inactive on device
            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}/apps/{AppId}", new Dictionary<string, object>
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
                ["username"] = _username,
                ["timestamp"] = now,
                ["app_version"] = GetAppVersion()
            };
            if (data != null)
            {
                foreach (var kvp in data)
                    eventData[kvp.Key] = kvp.Value;
            }
            await PostDataAsync($"events/{AppId}/{GetEventMonth()}", eventData);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to log usage event: {ex.Message}");
        }
    }

    public async Task LogErrorAsync(Exception ex, string context, string appVersion)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return;
        }

        try
        {
            var errorData = new Dictionary<string, object>
            {
                ["device_id"] = _deviceInfo.DeviceId,
                ["username"] = _username,
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["error_type"] = ex.GetType().Name,
                ["error_message"] = ex.Message,
                ["stack_trace"] = ex.StackTrace ?? "",
                ["context"] = context,
                ["app_version"] = appVersion
            };

            await PostDataAsync($"errors/{AppId}/{GetEventMonth()}", errorData);

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
                ["username"] = _username,
                ["current_version"] = currentVersion,
                ["latest_version"] = latestVersion,
                ["update_available"] = updateInfo.UpdateAvailable,
                ["timestamp"] = now
            };

            InfraLogger.Log("FirebaseService: Logging update check to Firebase");
            await PostDataAsync($"events/{AppId}/{GetEventMonth()}", checkData);

            // Also update the device record with last update check info
            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}/apps/{AppId}", new Dictionary<string, object>
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

    public async Task<bool> IsUserAdminAsync(string? windowsUsername = null)
    {
        if (!_isInitialized || _httpClient == null)
            return false;

        try
        {
            var username = (windowsUsername ?? Environment.UserName).ToLowerInvariant();
            var adminFlag = await GetDataAsync<object>($"admin_users/{username}");
            if (adminFlag != null)
            {
                var isAdmin = ParseBool(adminFlag.ToString()) ?? false;
                InfraLogger.Log($"Firebase: Admin check for '{username}' = {isAdmin}");
                return isAdmin;
            }

            InfraLogger.Log($"Firebase: Admin check for '{username}' = false (not found)");
            return false;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Admin check failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsCheatSheetEditorAsync(string? windowsUsername = null)
    {
        if (!_isInitialized || _httpClient == null)
            return false;

        try
        {
            var username = (windowsUsername ?? Environment.UserName).ToLowerInvariant();

            // Check dedicated editor role first
            var editorFlag = await GetDataAsync<object>($"cheat_sheet_editors/{username}");
            if (editorFlag != null)
            {
                var isEditor = ParseBool(editorFlag.ToString()) ?? false;
                if (isEditor)
                {
                    InfraLogger.Log($"Firebase: CheatSheet editor check for '{username}' = true (editor role)");
                    return true;
                }
            }

            // Fall back to admin check — admins automatically have editor privileges
            var adminFlag = await GetDataAsync<object>($"admin_users/{username}");
            if (adminFlag != null)
            {
                var isAdmin = ParseBool(adminFlag.ToString()) ?? false;
                if (isAdmin)
                {
                    InfraLogger.Log($"Firebase: CheatSheet editor check for '{username}' = true (admin role)");
                    return true;
                }
            }

            InfraLogger.Log($"Firebase: CheatSheet editor check for '{username}' = false");
            return false;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: CheatSheet editor check failed: {ex.Message}");
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
                ["username"] = _username,
                ["version"] = version,
                ["timestamp"] = now
            };

            await PostDataAsync($"events/{AppId}/{GetEventMonth()}", installData);

            // Update installed version on device
            await PatchDataAsync($"devices/{_deviceInfo?.DeviceId}/apps/{AppId}", new Dictionary<string, object>
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

    private async Task<string?> GetAccessTokenAsync()
    {
        if (_firebaseApp == null) return null;
        
        try
        {
            var credential = _firebaseApp.Options.Credential;
            
            // Add timeout to prevent hanging
            var tokenTask = ((GoogleCredential)credential).UnderlyingCredential.GetAccessTokenForRequestAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            
            var completedTask = await Task.WhenAny(tokenTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                InfraLogger.Log("Firebase: Access token request timed out");
                return null;
            }
            
            return await tokenTask;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to get access token: {ex.Message}");
            return null;
        }
    }

    private async Task PutDataAsync(string path, object data)
    {
        if (_httpClient == null || string.IsNullOrEmpty(_databaseUrl)) return;

        var token = await GetAccessTokenAsync();
        var url = $"{_databaseUrl}/{path}.json?access_token={token}";
        var json = JsonConvert.SerializeObject(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PutAsync(url, content);
    }

    private async Task PostDataAsync(string path, object data)
    {
        if (_httpClient == null || string.IsNullOrEmpty(_databaseUrl)) return;

        var token = await GetAccessTokenAsync();
        var url = $"{_databaseUrl}/{path}.json?access_token={token}";
        var json = JsonConvert.SerializeObject(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(url, content);
    }

    private async Task<T?> GetDataAsync<T>(string path)
    {
        if (_httpClient == null || string.IsNullOrEmpty(_databaseUrl)) return default;

        var token = await GetAccessTokenAsync();
        var url = $"{_databaseUrl}/{path}.json?access_token={token}";
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode) return default;
        
        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(json) || json == "null") return default;
        
        return JsonConvert.DeserializeObject<T>(json);
    }

    private async Task PatchDataAsync(string path, object data)
    {
        if (_httpClient == null || string.IsNullOrEmpty(_databaseUrl)) return;

        var token = await GetAccessTokenAsync();
        var url = $"{_databaseUrl}/{path}.json?access_token={token}";
        var json = JsonConvert.SerializeObject(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
        await _httpClient.SendAsync(request);
    }

    // --- Metrics sync for admin multi-user view ---

    public async Task SyncDailyMetricsAsync(string date, Dictionary<string, object> data)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return;

        try
        {
            await PutDataAsync($"metrics/{_deviceInfo.DeviceId}/{date}", data);
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
            return await GetDataAsync<Dictionary<string, Dictionary<string, Dictionary<string, object>>>>("metrics");
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
            return await GetDataAsync<Dictionary<string, Dictionary<string, object>>>("devices");
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
            await CleanupOldMonthNodes($"events/{AppId}", cutoffMonth);

            // Clean up errors/ months
            await CleanupOldMonthNodes($"errors/{AppId}", cutoffMonth);
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
        if (_httpClient == null || string.IsNullOrEmpty(_databaseUrl)) return;

        var token = await GetAccessTokenAsync();
        var url = $"{_databaseUrl}/{path}.json?access_token={token}";
        await _httpClient.DeleteAsync(url);
    }

}

