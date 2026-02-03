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
    private readonly bool _useOrganizedPaths = false; // Set to true to use new structure
    private FirebaseApp? _firebaseApp;
    private HttpClient? _httpClient;
    private string? _databaseUrl;
    private DeviceInfo? _deviceInfo;
    private System.Threading.Timer? _heartbeatTimer;
    private DateTime _sessionStartTime;
    private bool _isInitialized;

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
            var activationData = new Dictionary<string, object>
            {
                ["app_id"] = AppId,
                ["license_key"] = licenseKey ?? "FREE-AUTO",
                ["device_id"] = _deviceInfo.DeviceId,
                ["device_name"] = _deviceInfo.DeviceName,
                ["activated_at"] = DateTime.UtcNow.ToString("o"),
                ["last_validated"] = DateTime.UtcNow.ToString("o"),
                ["app_version"] = GetAppVersion(),
                ["device_info"] = _deviceInfo.ToDictionary()
            };

            await PutDataAsync($"device_activations/{_deviceInfo.DeviceId}", activationData);

            InfraLogger.Log($"Firebase: Device registered - {_deviceInfo.DeviceId}");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"Firebase: Failed to register device: {ex.Message}");
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
            var licenseKey = await GetLicenseKeyAsync();
            var heartbeatData = new Dictionary<string, object>
            {
                ["app_id"] = AppId,
                ["device_id"] = _deviceInfo.DeviceId,
                ["device_name"] = _deviceInfo.DeviceName,
                ["license_key"] = licenseKey ?? "FREE-AUTO",
                ["last_seen"] = DateTime.UtcNow.ToString("o"),
                ["status"] = "active",
                ["session_start"] = _sessionStartTime.ToString("o"),
                ["app_version"] = GetAppVersion(),
                ["device_info"] = _deviceInfo.ToDictionary()
            };

            await PutDataAsync($"device_heartbeats/{_deviceInfo.DeviceId}", heartbeatData);
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
                ["email"] = null!
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

    public async Task LogAppLaunchAsync(string appVersion)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return;
        }

        try
        {
            var licenseKey = await GetLicenseKeyAsync();
            var userId = DeviceIdentifier.GetUserIdentifier(_deviceInfo.DeviceId, licenseKey);

            var launchData = new Dictionary<string, object>
            {
                ["app_id"] = AppId,
                ["device_id"] = _deviceInfo.DeviceId,
                ["user_id"] = userId,
                ["license_key"] = licenseKey ?? "FREE-AUTO",
                ["mac_address"] = _deviceInfo.MacAddress ?? "unknown",
                ["device_info"] = _deviceInfo.ToDictionary(),
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["app_version"] = appVersion,
                ["event_type"] = "app_launch"
            };

            await PostDataAsync("app_launches", launchData);

            InfraLogger.Log($"Firebase: App launch logged");
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
            var licenseKey = await GetLicenseKeyAsync();
            var userId = DeviceIdentifier.GetUserIdentifier(_deviceInfo.DeviceId, licenseKey);

            var closeData = new Dictionary<string, object>
            {
                ["app_id"] = AppId,
                ["device_id"] = _deviceInfo.DeviceId,
                ["user_id"] = userId,
                ["license_key"] = licenseKey ?? "FREE-AUTO",
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["app_version"] = GetAppVersion(),
                ["event_type"] = "app_close",
                ["session_duration_seconds"] = (int)sessionDuration.TotalSeconds
            };

            await PostDataAsync("app_launches", closeData);

            await PatchDataAsync($"device_heartbeats/{_deviceInfo.DeviceId}", new Dictionary<string, object>
            {
                ["status"] = "inactive",
                ["last_seen"] = DateTime.UtcNow.ToString("o")
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
            var licenseKey = await GetLicenseKeyAsync();
            var userId = DeviceIdentifier.GetUserIdentifier(_deviceInfo.DeviceId, licenseKey);

            var eventData = new Dictionary<string, object>
            {
                ["app_id"] = AppId,
                ["device_id"] = _deviceInfo.DeviceId,
                ["user_id"] = userId,
                ["license_key"] = licenseKey ?? "FREE-AUTO",
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["app_version"] = GetAppVersion(),
                ["event_type"] = eventType
            };

            if (data != null)
            {
                foreach (var kvp in data)
                {
                    eventData[kvp.Key] = kvp.Value;
                }
            }

            await PostDataAsync("processing_sessions", eventData);
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
                ["app_id"] = AppId,
                ["device_id"] = _deviceInfo.DeviceId,
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["error_type"] = ex.GetType().Name,
                ["error_message"] = ex.Message,
                ["stack_trace"] = ex.StackTrace ?? "",
                ["context"] = context,
                ["app_version"] = appVersion
            };

            await PostDataAsync("error_logs", errorData);

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

            var checkData = new Dictionary<string, object>
            {
                ["app_id"] = AppId,
                ["device_id"] = _deviceInfo.DeviceId,
                ["current_version"] = currentVersion,
                ["latest_version"] = latestVersion,
                ["update_available"] = updateInfo.UpdateAvailable,
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            InfraLogger.Log("FirebaseService: Logging update check to Firebase");
            await PostDataAsync("update_checks", checkData);

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

    public async Task LogUpdateInstalledAsync(string version)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null)
        {
            return;
        }

        try
        {
            var installData = new Dictionary<string, object>
            {
                ["app_id"] = AppId,
                ["device_id"] = _deviceInfo.DeviceId,
                ["version"] = version,
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["event_type"] = "update_installed"
            };

            await PostDataAsync("processing_sessions", installData);

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

    private string GetOrganizedPath(string pathType)
    {
        if (!_useOrganizedPaths)
        {
            // Legacy paths
            return pathType;
        }

        // New organized structure
        var now = DateTime.UtcNow;
        var datePrefix = $"{now.Year:D4}/{now.Month:D2}/{now.Day:D2}";
        
        return pathType switch
        {
            "app_launches" => $"apps/{AppId}/analytics/launches/{datePrefix}",
            "device_activations" => $"apps/{AppId}/devices",
            "device_heartbeats" => $"apps/{AppId}/devices",
            "error_logs" => $"apps/{AppId}/analytics/errors/{datePrefix}",
            "processing_sessions" => $"apps/{AppId}/analytics/sessions/{datePrefix}",
            "update_checks" => $"apps/{AppId}/analytics/update_checks/{datePrefix}",
            _ => pathType
        };
    }
}

