using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DesktopHub.UI.Widgets;

/// <summary>
/// Pure C# admin operations — replaces all PowerShell script calls.
/// Every operation goes through IFirebaseService (already authenticated).
/// No scripts, no credentials on disk.
/// </summary>
public partial class DeveloperPanelWidget
{
    // ════════════════════════════════════════════════════════════
    // ROLE MANAGEMENT  (admin_users, dev_users, cheat_sheet_editors)
    // ════════════════════════════════════════════════════════════

    internal async Task SetRoleAsync(string node, string username, bool grant)
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        username = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(username))
        {
            AppendOutput("Username is required.");
            return;
        }

        try
        {
            if (grant)
            {
                await _firebaseService.SetNodeAsync($"{node}/{username}", true);
                AppendOutput($"Granted {node} role to '{username}'.");
            }
            else
            {
                await _firebaseService.DeleteNodeAsync($"{node}/{username}");
                AppendOutput($"Revoked {node} role from '{username}'.");
            }
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════
    // VERSION MANAGEMENT
    // ════════════════════════════════════════════════════════════

    internal async Task PublishVersionAsync(string appId, string version, string releaseNotes)
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        try
        {
            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var data = new Dictionary<string, object>
            {
                ["latest_version"] = version,
                ["release_date"] = now,
                ["release_notes"] = releaseNotes,
                ["download_url"] = $"https://github.com/Szmjuly/desktop-widgets/releases/download/v{version}/DesktopHub.exe",
                ["required_update"] = false,
                ["updated_at"] = now
            };

            await _firebaseService.SetNodeAsync($"app_versions/{appId}", data);
            AppendOutput($"Published {appId} v{version}.");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR publishing version: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════
    // DEVICE LISTING
    // ════════════════════════════════════════════════════════════

    internal async Task ListDevicesAndVersionsAsync(string appId = "desktophub")
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        try
        {
            var versionNode = await _firebaseService.GetNodeAsync($"app_versions/{appId}");
            var latestVersion = GetStringProp(versionNode, "latest_version") ?? "?";
            AppendOutput($"Latest {appId} version: {latestVersion}");

            var devices = await _firebaseService.GetNodeAsync("devices");
            if (devices == null || devices.Count == 0)
            {
                AppendOutput("No devices found.");
                return;
            }

            var outdatedCount = 0;
            foreach (var (deviceId, deviceObj) in devices)
            {
                var dev = ToDict(deviceObj);
                var username = GetStringProp(dev, "username") ?? "?";
                var deviceName = GetStringProp(dev, "device_name") ?? "?";
                var installed = GetAppInstalledVersion(dev, appId);
                var outdated = IsOutdated(installed, latestVersion);
                if (outdated) outdatedCount++;

                var tag = outdated ? " [OUTDATED]" : "";
                var shortId = deviceId.Length > 12 ? deviceId[..12] + "..." : deviceId;
                AppendOutput($"  {shortId}  {username,-12} {deviceName,-14} v{installed}{tag}");
            }

            AppendOutput($"Total: {devices.Count} devices, {outdatedCount} outdated");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════
    // PUSH UPDATES
    // ════════════════════════════════════════════════════════════

    internal async Task PushUpdateToDeviceAsync(string deviceId, string appId = "desktophub", string? targetVersion = null, bool force = false)
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        try
        {
            var device = await _firebaseService.GetNodeAsync($"devices/{deviceId}");
            if (device == null)
            {
                AppendOutput($"ERROR: Device '{deviceId}' not found.");
                return;
            }

            var versionNode = await _firebaseService.GetNodeAsync($"app_versions/{appId}");
            var latestVersion = GetStringProp(versionNode, "latest_version") ?? "0.0.0";
            var downloadUrl = GetStringProp(versionNode, "download_url") ?? "";
            var resolvedTarget = string.IsNullOrWhiteSpace(targetVersion) ? latestVersion : targetVersion;

            var installed = GetAppInstalledVersion(device, appId);
            var username = GetStringProp(device, "username") ?? "unknown";

            if (!force && !IsOutdated(installed, resolvedTarget))
            {
                AppendOutput($"Device '{deviceId}' ({username}) is already on v{installed} (target: {resolvedTarget}). Use force to push anyway.");
                return;
            }

            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var pushData = new Dictionary<string, object>
            {
                ["app_id"] = appId,
                ["target_version"] = resolvedTarget,
                ["download_url"] = downloadUrl,
                ["pushed_by"] = Environment.UserName?.ToLowerInvariant() ?? "unknown",
                ["pushed_at"] = now,
                ["status"] = "pending",
                ["status_updated_at"] = now,
                ["retry_count"] = 0
            };

            await _firebaseService.SetNodeAsync($"force_update/{deviceId}", pushData);
            AppendOutput($"Pushed v{resolvedTarget} to {username} ({deviceId[..Math.Min(12, deviceId.Length)]}...)");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR pushing update: {ex.Message}");
        }
    }

    internal async Task PushUpdateToAllAsync(string appId = "desktophub")
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        try
        {
            var versionNode = await _firebaseService.GetNodeAsync($"app_versions/{appId}");
            var latestVersion = GetStringProp(versionNode, "latest_version") ?? "0.0.0";
            var downloadUrl = GetStringProp(versionNode, "download_url") ?? "";

            var devices = await _firebaseService.GetNodeAsync("devices");
            if (devices == null || devices.Count == 0)
            {
                AppendOutput("No devices found.");
                return;
            }

            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var pushedBy = Environment.UserName?.ToLowerInvariant() ?? "unknown";
            var pushed = 0;

            foreach (var (deviceId, deviceObj) in devices)
            {
                var dev = ToDict(deviceObj);
                var installed = GetAppInstalledVersion(dev, appId);
                if (!IsOutdated(installed, latestVersion)) continue;

                var username = GetStringProp(dev, "username") ?? "unknown";

                var pushData = new Dictionary<string, object>
                {
                    ["app_id"] = appId,
                    ["target_version"] = latestVersion,
                    ["download_url"] = downloadUrl,
                    ["pushed_by"] = pushedBy,
                    ["pushed_at"] = now,
                    ["status"] = "pending",
                    ["status_updated_at"] = now,
                    ["retry_count"] = 0
                };

                try
                {
                    await _firebaseService.SetNodeAsync($"force_update/{deviceId}", pushData);
                    pushed++;
                    AppendOutput($"  Pushed to {username} ({deviceId[..Math.Min(8, deviceId.Length)]}...)");
                }
                catch (Exception ex)
                {
                    AppendOutput($"  FAILED: {username} — {ex.Message}");
                }
            }

            AppendOutput($"Pushed to {pushed} device(s).");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
        }
    }

    internal async Task GetUpdateStatusAsync(string appId = "desktophub")
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        try
        {
            var forceUpdates = await _firebaseService.GetNodeAsync("force_update");
            if (forceUpdates == null || forceUpdates.Count == 0)
            {
                AppendOutput("No force-update entries.");
                return;
            }

            foreach (var (deviceId, entryObj) in forceUpdates)
            {
                var entry = ToDict(entryObj);
                var entryApp = GetStringProp(entry, "app_id") ?? "";
                if (!string.IsNullOrWhiteSpace(entryApp) && !entryApp.Equals(appId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var status = GetStringProp(entry, "status") ?? "?";
                var target = GetStringProp(entry, "target_version") ?? "?";
                var pushedBy = GetStringProp(entry, "pushed_by") ?? "?";
                var pushedAt = GetStringProp(entry, "pushed_at") ?? "?";
                var error = GetStringProp(entry, "error") ?? "";

                var device = await _firebaseService.GetNodeAsync($"devices/{deviceId}");
                var username = GetStringProp(device, "username") ?? "?";

                var shortId = deviceId.Length > 12 ? deviceId[..12] + "..." : deviceId;
                AppendOutput($"  {shortId} ({username})  status={status}  target=v{target}  by={pushedBy}");
                if (!string.IsNullOrWhiteSpace(error))
                    AppendOutput($"    error: {error}");
            }
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
        }
    }

    internal async Task ClearCompletedUpdatesAsync(string appId = "desktophub")
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        try
        {
            var forceUpdates = await _firebaseService.GetNodeAsync("force_update");
            if (forceUpdates == null || forceUpdates.Count == 0)
            {
                AppendOutput("No force-update entries to clear.");
                return;
            }

            var cleared = 0;
            foreach (var (deviceId, entryObj) in forceUpdates)
            {
                var entry = ToDict(entryObj);
                var entryApp = GetStringProp(entry, "app_id") ?? "";
                if (!string.IsNullOrWhiteSpace(entryApp) && !entryApp.Equals(appId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var status = GetStringProp(entry, "status") ?? "";
                if (status is "completed" or "failed")
                {
                    await _firebaseService.DeleteNodeAsync($"force_update/{deviceId}");
                    cleared++;
                    var shortId = deviceId.Length > 8 ? deviceId[..8] + "..." : deviceId;
                    AppendOutput($"  Cleared: {shortId} (was: {status})");
                }
            }

            AppendOutput(cleared == 0 ? "No completed/failed entries to clear." : $"Cleared {cleared} entries.");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════
    // DATABASE OPERATIONS
    // ════════════════════════════════════════════════════════════

    internal async Task DumpDatabaseAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        try
        {
            AppendOutput("Fetching database structure...");
            foreach (var node in KnownNodes)
            {
                var data = await _firebaseService.GetNodeAsync(node);
                var count = data?.Count ?? 0;
                AppendOutput($"  {node}: {count} entries");
            }
            AppendOutput("Done.");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
        }
    }

    internal async Task BackupDatabaseAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        try
        {
            AppendOutput("Backing up database...");
            var backup = new Dictionary<string, object?>();
            foreach (var node in KnownNodes)
            {
                backup[node] = await _firebaseService.GetNodeAsync(node);
            }

            var backupDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopHub", "backups");
            System.IO.Directory.CreateDirectory(backupDir);

            var fileName = $"db-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            var filePath = System.IO.Path.Combine(backupDir, fileName);
            var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(filePath, json);

            AppendOutput($"Backup saved: {filePath}");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
        }
    }

    internal async Task ListTagsAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        try
        {
            var tags = await _firebaseService.GetNodeAsync("project_tags");
            if (tags == null || tags.Count == 0)
            {
                AppendOutput("No project tags found.");
                return;
            }

            AppendOutput($"Project tags: {tags.Count} entries");
            foreach (var (hash, _) in tags.Take(50))
            {
                AppendOutput($"  {hash}");
            }
            if (tags.Count > 50)
                AppendOutput($"  ... and {tags.Count - 50} more");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
        }
    }

    internal async Task DecryptTagsAsync()
    {
        AppendOutput("Decrypt tags requires the TagValueEncryptor — use the Database tab tree view to inspect tag data.");
        await Task.CompletedTask;
    }

    internal void ResetLocalMetrics()
    {
        try
        {
            var metricsDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopHub");

            var deleted = 0;
            foreach (var pattern in new[] { "metrics*.db", "metrics*.db-wal", "metrics*.db-shm" })
            {
                foreach (var file in System.IO.Directory.GetFiles(metricsDir, pattern))
                {
                    try { System.IO.File.Delete(file); deleted++; } catch { }
                }
            }

            AppendOutput(deleted > 0 ? $"Deleted {deleted} metrics file(s). Restart app to take effect." : "No metrics files found.");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
        }
    }

    internal void ShowHmacSecret()
    {
        try
        {
            var secret = DesktopHub.Infrastructure.Firebase.Utilities.ProjectHasher.ExportSecretBase64();
            AppendOutput($"HMAC Secret (Base64): {secret}");
        }
        catch (Exception ex)
        {
            AppendOutput($"HMAC Secret not available: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════
    // DEVICE CLEANUP
    // ════════════════════════════════════════════════════════════

    internal async Task CleanupDuplicateDevicesAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not available.");
            return;
        }

        try
        {
            var devices = await _firebaseService.GetNodeAsync("devices");
            if (devices == null || devices.Count == 0)
            {
                AppendOutput("No devices found.");
                return;
            }

            // Group by (username, mac_address) — duplicates share both
            var grouped = new Dictionary<string, List<(string DeviceId, string LastSeen)>>();
            foreach (var (deviceId, deviceObj) in devices)
            {
                var dev = ToDict(deviceObj);
                var username = GetStringProp(dev, "username") ?? "unknown";
                var mac = GetStringProp(dev, "mac_address") ?? "unknown";
                var lastSeen = GetStringProp(dev, "last_seen") ?? "";

                var key = $"{username}|{mac}";
                if (!grouped.ContainsKey(key))
                    grouped[key] = new List<(string, string)>();
                grouped[key].Add((deviceId, lastSeen));
            }

            var deleted = 0;
            foreach (var (key, dupes) in grouped)
            {
                if (dupes.Count <= 1) continue;

                // Keep the most recently seen device, delete the rest
                var sorted = dupes.OrderByDescending(d => d.LastSeen).ToList();
                var keeper = sorted[0];

                for (var i = 1; i < sorted.Count; i++)
                {
                    var stale = sorted[i];
                    var dev = ToDict(devices.GetValueOrDefault(stale.DeviceId));
                    var username = GetStringProp(dev, "username") ?? "unknown";

                    await _firebaseService.DeleteNodeAsync($"devices/{stale.DeviceId}");
                    await _firebaseService.DeleteNodeAsync($"users/{username}/devices/{stale.DeviceId}");
                    await _firebaseService.DeleteNodeAsync($"force_update/{stale.DeviceId}");
                    deleted++;

                    var shortId = stale.DeviceId.Length > 12 ? stale.DeviceId[..12] + "..." : stale.DeviceId;
                    AppendOutput($"  Removed stale device: {shortId} ({username})");
                }
            }

            AppendOutput(deleted == 0
                ? "No duplicate devices found."
                : $"Cleaned up {deleted} stale device(s).");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════

    private static Dictionary<string, object>? ToDict(object? obj)
    {
        if (obj is Dictionary<string, object> d) return d;
        if (obj is JsonElement je && je.ValueKind == JsonValueKind.Object)
            return je.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value);
        return null;
    }

    private static string? GetStringProp(Dictionary<string, object>? dict, string key)
    {
        if (dict == null || !dict.TryGetValue(key, out var val)) return null;
        if (val is JsonElement je) return je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString();
        return val?.ToString();
    }

    private static string GetAppInstalledVersion(Dictionary<string, object>? device, string appId)
    {
        if (device == null) return "0.0.0";
        if (!device.TryGetValue("apps", out var appsObj)) return "0.0.0";
        var apps = ToDict(appsObj);
        if (apps == null || !apps.TryGetValue(appId, out var appObj)) return "0.0.0";
        var app = ToDict(appObj);
        return GetStringProp(app, "installed_version") ?? "0.0.0";
    }

    private static bool IsOutdated(string installed, string latest)
    {
        if (!Version.TryParse(installed, out var iv)) return false;
        if (!Version.TryParse(latest, out var lv)) return false;
        return lv > iv;
    }
}
