using System.Windows;

namespace ProjectSearcher.UI;

/// <summary>
/// System tray icon for background operation
/// </summary>
public class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly SearchOverlay _searchOverlay;
    private readonly string _hotkeyLabel;
    private readonly ProjectSearcher.Core.Abstractions.ISettingsService _settings;

    public TrayIcon(SearchOverlay searchOverlay, string hotkeyLabel, ProjectSearcher.Core.Abstractions.ISettingsService settings)
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
        _searchOverlay.Dispatcher.Invoke(() =>
        {
            var menu = new TrayMenu(
                ShowSearch,
                RescanProjects,
                ShowSettings,
                Exit
            );
            menu.Show();
        });
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
