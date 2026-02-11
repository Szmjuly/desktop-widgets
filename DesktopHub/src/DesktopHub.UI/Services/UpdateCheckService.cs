using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DesktopHub.Core.Abstractions;
using DesktopHub.Infrastructure.Firebase.Models;

namespace DesktopHub.UI.Services;

public class UpdateCheckService
{
    private readonly ISettingsService _settings;
    private readonly Func<Task<UpdateInfo?>> _checkForUpdatesFunc;
    private System.Threading.Timer? _timer;
    private UpdateInfo? _latestUpdateInfo;
    private bool _isUpdateAvailable;

    public event EventHandler<UpdateInfo>? UpdateAvailable;
    public event EventHandler? UpdateDismissed;

    public bool IsUpdateAvailable => _isUpdateAvailable;
    public UpdateInfo? LatestUpdateInfo => _latestUpdateInfo;

    public UpdateCheckService(ISettingsService settings, Func<Task<UpdateInfo?>> checkForUpdatesFunc)
    {
        _settings = settings;
        _checkForUpdatesFunc = checkForUpdatesFunc;
    }

    public void Start()
    {
        if (!_settings.GetAutoUpdateCheckEnabled())
        {
            DebugLogger.Log("UpdateCheckService: Auto update check is disabled, not starting timer");
            return;
        }

        var frequencyMinutes = _settings.GetUpdateCheckFrequencyMinutes();
        var interval = TimeSpan.FromMinutes(Math.Max(frequencyMinutes, 30)); // Minimum 30 minutes

        // Initial delay: 15 seconds after app start — quick enough to notify on boot
        var initialDelay = TimeSpan.FromSeconds(15);

        _timer?.Dispose();
        _timer = new System.Threading.Timer(async _ => await PerformCheckAsync(), null, initialDelay, interval);

        DebugLogger.Log($"UpdateCheckService: Started with initial delay {initialDelay.TotalMinutes}min, interval {interval.TotalMinutes}min");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        DebugLogger.Log("UpdateCheckService: Stopped");
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public async Task ForceCheckAsync()
    {
        DebugLogger.Log("UpdateCheckService: Force check requested");
        await PerformCheckAsync();
    }

    public void DismissUpdate()
    {
        _isUpdateAvailable = false;
        _latestUpdateInfo = null;
        UpdateDismissed?.Invoke(this, EventArgs.Empty);
        DebugLogger.Log("UpdateCheckService: Update dismissed");
    }

    private async Task PerformCheckAsync()
    {
        try
        {
            if (!_settings.GetAutoUpdateCheckEnabled())
            {
                DebugLogger.Log("UpdateCheckService: Skipping check - auto update disabled");
                return;
            }

            DebugLogger.Log("UpdateCheckService: Performing update check...");
            var updateInfo = await _checkForUpdatesFunc();

            if (updateInfo != null && updateInfo.UpdateAvailable)
            {
                _isUpdateAvailable = true;
                _latestUpdateInfo = updateInfo;
                DebugLogger.Log($"UpdateCheckService: Update available! Current={updateInfo.CurrentVersion}, Latest={updateInfo.LatestVersion}");

                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateAvailable?.Invoke(this, updateInfo);
                }));
            }
            else
            {
                _isUpdateAvailable = false;
                _latestUpdateInfo = updateInfo;
                DebugLogger.Log("UpdateCheckService: No update available");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"UpdateCheckService: Check failed: {ex.Message}");
        }
    }
}
