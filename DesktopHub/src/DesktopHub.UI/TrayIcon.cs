using System.Windows;

namespace DesktopHub.UI;

/// <summary>
/// System tray icon for background operation
/// </summary>
public class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly SearchOverlay _searchOverlay;
    private readonly string _hotkeyLabel;
    private readonly DesktopHub.Core.Abstractions.ISettingsService _settings;

    public TrayIcon(SearchOverlay searchOverlay, string hotkeyLabel, DesktopHub.Core.Abstractions.ISettingsService settings)
    {
        _searchOverlay = searchOverlay;
        _hotkeyLabel = hotkeyLabel;
        _settings = settings;

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application, // TODO: Add custom icon
            Visible = true,
            Text = $"Project Searcher - Press {_hotkeyLabel} to search"
        };

        // Use custom styled menu instead of Windows Forms context menu
        // Don't set ContextMenuStrip to prevent default menu from appearing
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                ShowCustomMenu();
            }
        };
        
        _notifyIcon.DoubleClick += (s, e) => ShowSearch();

        // Show balloon tip on first run
        ShowBalloonTip("Project Searcher", $"Press {_hotkeyLabel} to search projects", System.Windows.Forms.ToolTipIcon.Info);
    }

    private void ShowBalloonTip(string title, string text, System.Windows.Forms.ToolTipIcon icon)
    {
        var duration = _settings.GetNotificationDurationMs();
        if (duration > 0)
        {
            _notifyIcon.ShowBalloonTip(duration, title, text, icon);
        }
    }

    private void ShowCustomMenu()
    {
        try
        {
            _searchOverlay.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var menu = new TrayMenu(
                        ShowSearch,
                        RescanProjects,
                        CheckForUpdates,
                        ShowSettings,
                        Exit
                    );
                    menu.Show();
                }
                catch (InvalidOperationException ex)
                {
                    DebugLogger.Log($"ShowCustomMenu: Failed to show menu (window closing conflict): {ex.Message}");
                    // Window was closing, silently ignore
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"ShowCustomMenu: Unexpected error: {ex}");
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"ShowCustomMenu: Failed to queue menu: {ex.Message}");
        }
    }

    private void ShowSearch()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _searchOverlay.ShowFromTray();
        }), System.Windows.Threading.DispatcherPriority.Normal);
    }

    private void RescanProjects()
    {
        ShowBalloonTip("Project Searcher", "Rescanning Q: drive...", System.Windows.Forms.ToolTipIcon.Info);

        // Trigger rescan
        Task.Run(async () =>
        {
            try
            {
                // Force rescan by clearing last scan time
                // This will be implemented in the SearchOverlay
                await _searchOverlay.Dispatcher.InvokeAsync(() =>
                {
                    // Trigger background scan
                });

                ShowBalloonTip("Project Searcher", "Rescan complete", System.Windows.Forms.ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Project Searcher", $"Rescan failed: {ex.Message}", System.Windows.Forms.ToolTipIcon.Error);
            }
        });
    }

    private static string FormatVersion(string version)
    {
        if (string.IsNullOrEmpty(version))
            return version;
        
        var parts = version.Split('.');
        if (parts.Length >= 3)
        {
            return $"{parts[0]}.{parts[1]}.{parts[2]}";
        }
        return version;
    }

    private void CheckForUpdates()
    {
        DebugLogger.Log("=== CHECK FOR UPDATES STARTED ===");

        Task.Run(async () =>
        {
            try
            {
                DebugLogger.Log("Update: Getting FirebaseManager instance");
                var app = System.Windows.Application.Current as App;
                var firebaseManager = app?.FirebaseManager;

                if (firebaseManager == null)
                {
                    DebugLogger.Log("Update: FirebaseManager is null - offline mode");
                    ShowBalloonTip("DesktopHub", "Update checking unavailable (offline mode)", System.Windows.Forms.ToolTipIcon.Warning);
                    return;
                }

                DebugLogger.Log("Update: Calling CheckForUpdatesAsync");
                var updateInfo = await firebaseManager.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    DebugLogger.Log("Update: CheckForUpdatesAsync returned null");
                    ShowBalloonTip("DesktopHub", "Could not check for updates", System.Windows.Forms.ToolTipIcon.Warning);
                    return;
                }

                DebugLogger.Log($"Update: Current={updateInfo.CurrentVersion}, Latest={updateInfo.LatestVersion}, Available={updateInfo.UpdateAvailable}");
                DebugLogger.Log($"Update: DownloadUrl={updateInfo.DownloadUrl}");
                DebugLogger.Log($"Update: ReleaseNotes={updateInfo.ReleaseNotes}");

                if (updateInfo.UpdateAvailable)
                {
                    var message = $"Version {FormatVersion(updateInfo.LatestVersion)} is available!\n\nCurrent: {FormatVersion(updateInfo.CurrentVersion)}";
                    if (!string.IsNullOrEmpty(updateInfo.ReleaseNotes))
                    {
                        message += $"\n\n{updateInfo.ReleaseNotes}";
                    }
                    message += "\n\nWould you like to download and install this update now?";

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var result = System.Windows.MessageBox.Show(
                            message,
                            "Update Available",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Information
                        );

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            DebugLogger.Log("Update: User confirmed update installation");
                            Task.Run(async () => await DownloadAndInstallUpdateAsync(updateInfo));
                        }
                        else
                        {
                            DebugLogger.Log("Update: User declined update installation");
                        }
                    });
                }
                else
                {
                    ShowBalloonTip("DesktopHub", $"You're up to date! (v{FormatVersion(updateInfo.CurrentVersion)})", System.Windows.Forms.ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Update: EXCEPTION in CheckForUpdates: {ex.Message}");
                DebugLogger.Log($"Update: Stack trace: {ex.StackTrace}");
                ShowBalloonTip("DesktopHub", "Update check failed", System.Windows.Forms.ToolTipIcon.Error);
            }
        });
    }

    private async Task DownloadAndInstallUpdateAsync(Infrastructure.Firebase.Models.UpdateInfo updateInfo)
    {
        try
        {
            DebugLogger.Log("=== DOWNLOAD AND INSTALL UPDATE STARTED ===");
            DebugLogger.Log($"Update: Target version: {updateInfo.LatestVersion}");
            DebugLogger.Log($"Update: Download URL: {updateInfo.DownloadUrl}");
            
            if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            {
                DebugLogger.Log("Update: ERROR - Download URL is empty");
                ShowBalloonTip("DesktopHub", "Update download URL is missing", System.Windows.Forms.ToolTipIcon.Error);
                return;
            }

            DebugLogger.Log($"Update: Starting download from {updateInfo.DownloadUrl}");
            ShowBalloonTip("DesktopHub", $"Downloading v{FormatVersion(updateInfo.LatestVersion)}...", System.Windows.Forms.ToolTipIcon.Info);

            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"DesktopHub-{updateInfo.LatestVersion}.exe");

            using (var client = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler 
            { 
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            }))
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.Add("User-Agent", "DesktopHub-AutoUpdater/1.0");
                
                DebugLogger.Log($"Update: Starting HTTP download from {updateInfo.DownloadUrl}");
                DebugLogger.Log($"Update: User-Agent: DesktopHub-AutoUpdater/1.0");
                DebugLogger.Log($"Update: AllowAutoRedirect: True, MaxRedirections: 10");
                
                var response = await client.GetAsync(updateInfo.DownloadUrl);
                
                DebugLogger.Log($"Update: HTTP Status: {response.StatusCode} ({(int)response.StatusCode})");
                DebugLogger.Log($"Update: Response URL: {response.RequestMessage?.RequestUri}");
                DebugLogger.Log($"Update: Content-Type: {response.Content.Headers.ContentType}");
                DebugLogger.Log($"Update: Content-Length: {response.Content.Headers.ContentLength}");
                
                if (response.Headers.Contains("Location"))
                {
                    DebugLogger.Log($"Update: Redirect Location: {response.Headers.GetValues("Location").FirstOrDefault()}");
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    DebugLogger.Log($"Update: Error response body (first 500 chars): {errorContent.Substring(0, Math.Min(500, errorContent.Length))}");
                }
                
                response.EnsureSuccessStatusCode();

                var updateData = await response.Content.ReadAsByteArrayAsync();
                DebugLogger.Log($"Update: Download complete, size: {updateData.Length} bytes");
                
                await System.IO.File.WriteAllBytesAsync(tempPath, updateData);
                DebugLogger.Log($"Update: Saved to {tempPath}");
            }

            DebugLogger.Log("Update: Download complete, preparing to install");
            ShowBalloonTip("DesktopHub", "Installing update...", System.Windows.Forms.ToolTipIcon.Info);

            var currentExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            
            DebugLogger.Log($"Update: Current exe path (initial): {currentExePath}");
            
            if (string.IsNullOrEmpty(currentExePath) || currentExePath.EndsWith(".dll"))
            {
                currentExePath = System.AppContext.BaseDirectory + "DesktopHub.exe";
                DebugLogger.Log($"Update: Using fallback path: {currentExePath}");
            }

            var updateBatchPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopHub-Update.bat");
            var batchContent = $@"@echo off
timeout /t 2 /nobreak > nul
taskkill /F /IM DesktopHub.exe > nul 2>&1
timeout /t 1 /nobreak > nul
copy /Y ""{tempPath}"" ""{currentExePath}"" > nul
del ""{tempPath}"" > nul
start """" ""{currentExePath}""
del ""%~f0""
";

            await System.IO.File.WriteAllTextAsync(updateBatchPath, batchContent);
            DebugLogger.Log($"Update: Created update script at {updateBatchPath}");

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var result = System.Windows.MessageBox.Show(
                    $"Update ready to install!\n\nDesktopHub will restart to complete the update.",
                    "Install Update",
                    System.Windows.MessageBoxButton.OKCancel,
                    System.Windows.MessageBoxImage.Information
                );

                if (result == System.Windows.MessageBoxResult.OK)
                {
                    DebugLogger.Log("Update: Starting update installer");
                    
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = updateBatchPath,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };

                    System.Diagnostics.Process.Start(startInfo);
                    
                    System.Windows.Application.Current.Shutdown();
                }
                else
                {
                    DebugLogger.Log("Update: User cancelled installation");
                    ShowBalloonTip("DesktopHub", "Update cancelled", System.Windows.Forms.ToolTipIcon.Info);
                }
            });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Update: Failed to download/install: {ex.Message}");
            DebugLogger.Log($"Update: Stack trace: {ex.StackTrace}");
            ShowBalloonTip("DesktopHub", "Update failed. Please try again later.", System.Windows.Forms.ToolTipIcon.Error);
        }
    }

    public void ShowSettings()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            var settings = new SettingsWindow(
                _settings,
                () => _searchOverlay.ReloadHotkey()
            );
            settings.Show();
        }), System.Windows.Threading.DispatcherPriority.Normal);
    }

    private void Exit()
    {
        var confirmed = _searchOverlay.Dispatcher.Invoke(() =>
        {
            return ConfirmationDialog.Show("Are you sure you want to exit Project Searcher?");
        });

        if (confirmed)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
