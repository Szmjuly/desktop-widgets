using System.Net.Http;
using System.Text;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using HAPExtractor.Infrastructure.Firebase.Models;
using HAPExtractor.Infrastructure.Firebase.Utilities;

namespace HAPExtractor.Infrastructure.Firebase;

public class HapFirebaseService : IHapFirebaseService
{
    private const string AppId = "hapextractor";
    private const string DefaultDatabaseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com";
    private readonly string _appDataDir;
    private FirebaseApp? _firebaseApp;
    private HttpClient? _httpClient;
    private string? _databaseUrl;
    private DeviceInfo? _deviceInfo;
    private System.Threading.Timer? _heartbeatTimer;
    private bool _isInitialized;
    private bool _forcedUpdateProcessing;
    private readonly string _username = Environment.UserName.ToLowerInvariant();

    public event EventHandler<ForcedUpdateInfo>? ForcedUpdateDetected;

    public bool IsInitialized => _isInitialized;

    public HapFirebaseService()
    {
        _appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HAPExtractor"
        );
        Directory.CreateDirectory(_appDataDir);
    }

    // ================================================================
    // INITIALIZATION
    // ================================================================

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            Log("Starting initialization...");

            var credential = await GetCredentialAsync();
            if (credential == null)
            {
                Log("No credentials found, running in offline mode");
                return;
            }

            _databaseUrl = DefaultDatabaseUrl;

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

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _deviceInfo = DeviceIdentifier.GetDeviceInfo();
            _isInitialized = true;

            Log($"Initialized successfully for device {_deviceInfo.DeviceId}");
        }
        catch (Exception ex)
        {
            Log($"Initialization failed: {ex.Message}");
            _isInitialized = false;
        }
    }

    private async Task<GoogleCredential?> GetCredentialAsync()
    {
        // Check external config file
        var configFile = Path.Combine(_appDataDir, "firebase-config.json");
        if (File.Exists(configFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(configFile);
                return GoogleCredential.FromJson(json);
            }
            catch (Exception ex)
            {
                Log($"Failed to load config from file: {ex.Message}");
            }
        }

        var embedded = GetEmbeddedServiceAccount();
        if (embedded != null)
        {
            try
            {
                return GoogleCredential.FromJson(embedded);
            }
            catch (Exception ex)
            {
                Log($"Failed to load embedded config: {ex.Message}");
            }
        }

        return null;
    }

    private string? GetEmbeddedServiceAccount()
    {
        try
        {
            // Embedded resource (for Release single-file builds)
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                var resourceName = "firebase-license.json";
                var resourceNames = assembly.GetManifestResourceNames();
                var matchingResource = resourceNames.FirstOrDefault(r => r.EndsWith(resourceName));

                if (matchingResource != null)
                {
                    using var stream = assembly.GetManifestResourceStream(matchingResource);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        return reader.ReadToEnd();
                    }
                }
            }

            // Fallback: application directory
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var appDirLicense = Path.Combine(appDir, "firebase-license.json");
            if (File.Exists(appDirLicense))
                return File.ReadAllText(appDirLicense);

            // Fallback: secrets/ folder (Debug builds)
            var solutionRoot = FindSolutionRoot(appDir);
            if (solutionRoot != null)
            {
                var licenseFile = Path.Combine(solutionRoot, "secrets", "firebase-license.json");
                if (File.Exists(licenseFile))
                    return File.ReadAllText(licenseFile);
            }

            return null;
        }
        catch (Exception ex)
        {
            Log($"Failed to read embedded service account: {ex.Message}");
            return null;
        }
    }

    private static string? FindSolutionRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Any() || dir.GetFiles("firebase-license.json").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    // ================================================================
    // DEVICE REGISTRATION
    // ================================================================

    public string GetDeviceId() => DeviceIdentifier.GetDeviceId();

    public async Task RegisterDeviceAsync()
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return;

        try
        {
            var appVersion = GetAppVersion();
            var now = DateTime.UtcNow.ToString("o");

            // Update users/{username}
            await PatchDataAsync($"users/{_username}", new Dictionary<string, object>
            {
                ["last_seen"] = now,
                ["display_name"] = Environment.UserName
            });
            await PutDataAsync($"users/{_username}/devices/{_deviceInfo.DeviceId}", true);

            // Update devices/{device_id}
            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}", new Dictionary<string, object>
            {
                ["device_name"] = _deviceInfo.DeviceName,
                ["username"] = _username,
                ["mac_address"] = _deviceInfo.MacAddress ?? "unknown",
                ["platform"] = "Windows",
                ["platform_version"] = Environment.OSVersion.Version.ToString(),
                ["machine"] = Environment.Is64BitOperatingSystem ? "x64" : "x86",
                ["last_seen"] = now,
                ["status"] = "active"
            });

            // Update per-app state: devices/{device_id}/apps/{app_id}
            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}/apps/{AppId}", new Dictionary<string, object>
            {
                ["installed_version"] = appVersion,
                ["last_launch"] = now,
                ["status"] = "active"
            });

            Log($"Device registered - {_deviceInfo.DeviceId} (user: {_username})");
        }
        catch (Exception ex)
        {
            Log($"Failed to register device: {ex.Message}");
        }
    }

    // ================================================================
    // HEARTBEAT
    // ================================================================

    public void StartHeartbeat()
    {
        if (!_isInitialized) return;

        _heartbeatTimer = new System.Threading.Timer(
            async _ => await UpdateHeartbeatAsync(),
            null,
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(10)
        );

        Log("Heartbeat started (every 10 minutes)");
    }

    public void StopHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
        Log("Heartbeat stopped");
    }

    public async Task UpdateHeartbeatAsync()
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return;

        try
        {
            var now = DateTime.UtcNow.ToString("o");
            var appVersion = GetAppVersion();

            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}", new Dictionary<string, object>
            {
                ["device_name"] = _deviceInfo.DeviceName,
                ["username"] = _username,
                ["mac_address"] = _deviceInfo.MacAddress ?? "unknown",
                ["last_seen"] = now,
                ["status"] = "active"
            });
            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}/apps/{AppId}", new Dictionary<string, object>
            {
                ["installed_version"] = appVersion,
                ["last_launch"] = now,
                ["status"] = "active"
            });
            await PatchDataAsync($"users/{_username}", new Dictionary<string, object>
            {
                ["last_seen"] = now
            });

            // Check for admin-pushed forced update
            if (!_forcedUpdateProcessing)
            {
                try
                {
                    var forcedUpdate = await CheckForForcedUpdateAsync();
                    if (forcedUpdate != null)
                    {
                        _forcedUpdateProcessing = true;
                        Log($"Forced update detected! Target: v{forcedUpdate.TargetVersion}");
                        ForcedUpdateDetected?.Invoke(this, forcedUpdate);
                    }
                }
                catch (Exception fuEx)
                {
                    Log($"Forced update check failed (non-fatal): {fuEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Heartbeat update failed: {ex.Message}");
        }
    }

    // ================================================================
    // UPDATE CHECKING
    // ================================================================

    public async Task<UpdateInfo?> CheckForUpdatesAsync(string currentVersion)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return null;

        try
        {
            var data = await GetDataAsync<Dictionary<string, object>>($"app_versions/{AppId}");
            if (data == null) return null;

            var latestVersion = data.GetValueOrDefault("latest_version")?.ToString();
            if (string.IsNullOrEmpty(latestVersion)) return null;

            var updateInfo = new UpdateInfo
            {
                LatestVersion = latestVersion,
                CurrentVersion = currentVersion,
                ReleaseNotes = data.GetValueOrDefault("release_notes")?.ToString(),
                DownloadUrl = data.GetValueOrDefault("download_url")?.ToString(),
                ReleaseDate = ParseDateTime(data.GetValueOrDefault("release_date")?.ToString()),
                RequiredUpdate = ParseBool(data.GetValueOrDefault("required_update")?.ToString()) ?? false
            };

            // Log the check
            var now = DateTime.UtcNow.ToString("o");
            await PostDataAsync($"events/{AppId}/{GetEventMonth()}", new Dictionary<string, object>
            {
                ["event_type"] = "update_check",
                ["device_id"] = _deviceInfo.DeviceId,
                ["username"] = _username,
                ["current_version"] = currentVersion,
                ["latest_version"] = latestVersion,
                ["update_available"] = updateInfo.UpdateAvailable,
                ["timestamp"] = now
            });

            await PatchDataAsync($"devices/{_deviceInfo.DeviceId}/apps/{AppId}", new Dictionary<string, object>
            {
                ["last_update_check"] = now,
                ["last_known_version"] = latestVersion
            });

            Log($"Update check: current={currentVersion}, latest={latestVersion}, available={updateInfo.UpdateAvailable}");
            return updateInfo;
        }
        catch (Exception ex)
        {
            Log($"CheckForUpdatesAsync failed: {ex.Message}");
            return null;
        }
    }

    // ================================================================
    // FORCED UPDATE
    // ================================================================

    public async Task<ForcedUpdateInfo?> CheckForForcedUpdateAsync()
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return null;

        try
        {
            var data = await GetDataAsync<Dictionary<string, object>>($"force_update/{_deviceInfo.DeviceId}");
            if (data == null) return null;

            // Filter by app_id — only process entries for this app
            var entryAppId = data.GetValueOrDefault("app_id")?.ToString();
            if (!string.IsNullOrEmpty(entryAppId) && entryAppId != AppId)
                return null;
            // If app_id is missing, treat as "desktophub" (backward compat) — skip for us
            if (string.IsNullOrEmpty(entryAppId))
                return null;

            var status = data.GetValueOrDefault("status")?.ToString();
            if (status != "pending") return null;

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
            Log($"CheckForForcedUpdateAsync failed: {ex.Message}");
            return null;
        }
    }

    public async Task UpdateForcedUpdateStatusAsync(string status, string? error = null)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return;

        try
        {
            var update = new Dictionary<string, object>
            {
                ["status"] = status,
                ["status_updated_at"] = DateTime.UtcNow.ToString("o")
            };

            if (!string.IsNullOrEmpty(error))
                update["error"] = error;

            if (status == "failed")
            {
                var data = await GetDataAsync<Dictionary<string, object>>($"force_update/{_deviceInfo.DeviceId}");
                var retryCount = 0;
                if (data?.TryGetValue("retry_count", out var rc) == true)
                    int.TryParse(rc?.ToString(), out retryCount);
                update["retry_count"] = retryCount + 1;

                if (retryCount + 1 < 3)
                {
                    update["status"] = "pending";
                    Log($"Forced update retry {retryCount + 1}/3");
                }
                else
                {
                    Log("Forced update failed after 3 retries");
                }
            }

            await PatchDataAsync($"force_update/{_deviceInfo.DeviceId}", update);
            Log($"Forced update status -> {update["status"]}");
        }
        catch (Exception ex)
        {
            Log($"UpdateForcedUpdateStatusAsync failed: {ex.Message}");
        }
    }

    public async Task CompleteForcedUpdateIfPendingAsync()
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return;

        try
        {
            var data = await GetDataAsync<Dictionary<string, object>>($"force_update/{_deviceInfo.DeviceId}");
            if (data == null) return;

            var entryAppId = data.GetValueOrDefault("app_id")?.ToString();
            if (!string.IsNullOrEmpty(entryAppId) && entryAppId != AppId) return;
            if (string.IsNullOrEmpty(entryAppId)) return;

            var status = data.GetValueOrDefault("status")?.ToString();
            if (status == "installing" || status == "downloading")
            {
                await PatchDataAsync($"force_update/{_deviceInfo.DeviceId}", new Dictionary<string, object>
                {
                    ["status"] = "completed",
                    ["status_updated_at"] = DateTime.UtcNow.ToString("o")
                });
                Log("Forced update marked as completed (post-restart)");
                _forcedUpdateProcessing = false;
            }
        }
        catch (Exception ex)
        {
            Log($"CompleteForcedUpdateIfPendingAsync failed: {ex.Message}");
        }
    }

    // ================================================================
    // TELEMETRY
    // ================================================================

    public async Task LogAppLaunchAsync(string appVersion)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return;

        try
        {
            await PostDataAsync($"events/{AppId}/{GetEventMonth()}", new Dictionary<string, object>
            {
                ["event_type"] = "app_launch",
                ["device_id"] = _deviceInfo.DeviceId,
                ["username"] = _username,
                ["app_version"] = appVersion,
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }
        catch (Exception ex)
        {
            Log($"Failed to log app launch: {ex.Message}");
        }
    }

    public async Task LogAppCloseAsync(TimeSpan sessionDuration)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return;

        try
        {
            await PostDataAsync($"events/{AppId}/{GetEventMonth()}", new Dictionary<string, object>
            {
                ["event_type"] = "app_close",
                ["device_id"] = _deviceInfo.DeviceId,
                ["username"] = _username,
                ["session_duration_seconds"] = (int)sessionDuration.TotalSeconds,
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }
        catch (Exception ex)
        {
            Log($"Failed to log app close: {ex.Message}");
        }
    }

    public async Task LogErrorAsync(Exception ex, string context, string appVersion)
    {
        if (!_isInitialized || _httpClient == null || _deviceInfo == null) return;

        try
        {
            await PostDataAsync($"errors/{AppId}/{GetEventMonth()}", new Dictionary<string, object>
            {
                ["device_id"] = _deviceInfo.DeviceId,
                ["username"] = _username,
                ["app_version"] = appVersion,
                ["error_type"] = ex.GetType().Name,
                ["error_message"] = ex.Message,
                ["stack_trace"] = ex.StackTrace ?? "",
                ["context"] = context,
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }
        catch
        {
            // Swallow — don't let error logging cause more errors
        }
    }

    // ================================================================
    // HTTP HELPERS
    // ================================================================

    private async Task<string?> GetAccessTokenAsync()
    {
        if (_firebaseApp == null) return null;

        try
        {
            var credential = _firebaseApp.Options.Credential;
            var tokenTask = ((GoogleCredential)credential).UnderlyingCredential.GetAccessTokenForRequestAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

            var completedTask = await Task.WhenAny(tokenTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                Log("Access token request timed out");
                return null;
            }

            return await tokenTask;
        }
        catch (Exception ex)
        {
            Log($"Failed to get access token: {ex.Message}");
            return null;
        }
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

    // ================================================================
    // UTILITIES
    // ================================================================

    private static string GetEventMonth() => DateTime.UtcNow.ToString("yyyy-MM");

    private static string GetAppVersion()
    {
        return System.Reflection.Assembly.GetEntryAssembly()?
            .GetName().Version?.ToString() ?? "1.0.0";
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return DateTime.TryParse(value, out var result) ? result : null;
    }

    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return bool.TryParse(value, out var result) ? result : null;
    }

    private static void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[HapFirebase] {message}");
    }
}
