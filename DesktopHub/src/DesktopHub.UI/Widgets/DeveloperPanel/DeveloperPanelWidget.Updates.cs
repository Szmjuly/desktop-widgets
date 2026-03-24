using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget
{
    // ════════════════════════════════════════════════════════════
    // UPDATES TAB
    // ════════════════════════════════════════════════════════════

    private void InitUpdatesTab()
    {
        UpdateVersionBox.Text = DateTime.Now.ToString("yyyy.M.d", CultureInfo.InvariantCulture);
        UpdateNotesBox.Text = "";
    }

    // ════════════════════════════════════════════════════════════
    // VERSION MANAGEMENT
    // ════════════════════════════════════════════════════════════

    private async void PublishVersion_Click(object sender, RoutedEventArgs e)
    {
        var version = UpdateVersionBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            AppendOutput("Version is required.");
            return;
        }

        var notes = UpdateNotesBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(notes))
            notes = "New version available";

        // Replace spaces with underscores for PowerShell arg safety
        var safeNotes = notes.Replace(" ", "_");

        if (!ConfirmDangerous($"Publish version {version}?")) return;

        await RunScriptAsync("admin.ps1", "-Action", "version-update", "-Version", version, "-ReleaseNotes", safeNotes);
    }

    // ════════════════════════════════════════════════════════════
    // PUSH UPDATES
    // ════════════════════════════════════════════════════════════

    private async void ListDevices_Click(object sender, RoutedEventArgs e)
    {
        var (stdout, _, exitCode) = await RunScriptWithOutputAsync("admin.ps1", false, "-Action", "update-list");
        if (exitCode == 0) ShowUpdateResults(stdout);
    }

    private async void PushUpdateAll_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDangerous("Push update to all outdated devices?")) return;
        await RunScriptAsync("admin.ps1", "-Action", "update-push-all");
    }

    private async void PushUpdateDevice_Click(object sender, RoutedEventArgs e)
    {
        var deviceId = PushDeviceIdBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            AppendOutput("Device ID is required.");
            return;
        }

        if (!ConfirmDangerous($"Push update to device '{deviceId}'?")) return;
        await RunScriptAsync("admin.ps1", "-Action", "update-push", "-DeviceId", deviceId);
    }

    private async void CheckUpdateStatus_Click(object sender, RoutedEventArgs e)
    {
        var (stdout, _, exitCode) = await RunScriptWithOutputAsync("admin.ps1", false, "-Action", "update-status");
        if (exitCode == 0) ShowUpdateResults(stdout);
    }

    private async void ClearUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDangerous("Clear completed/failed push update entries?")) return;
        await RunScriptAsync("admin.ps1", "-Action", "update-clear");
    }

    // ════════════════════════════════════════════════════════════
    // BUILD TOOLS
    // ════════════════════════════════════════════════════════════

    private async void Build_Click(object sender, RoutedEventArgs e)
    {
        await RunScriptAsync("admin.ps1", "-Action", "build");
    }

    private async void BuildInstaller_Click(object sender, RoutedEventArgs e)
    {
        await RunScriptAsync("admin.ps1", "-Action", "build-installer");
    }

    // ════════════════════════════════════════════════════════════
    // INLINE RESULTS
    // ════════════════════════════════════════════════════════════

    private void ShowUpdateResults(string stdout)
    {
        UpdateResultsPanel.Children.Clear();

        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
        {
            UpdateResultsBorder.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            var text = new TextBlock
            {
                Text = trimmed,
                FontSize = 11,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Foreground = FindBrush("TextPrimaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 2)
            };

            UpdateResultsPanel.Children.Add(text);
        }

        UpdateResultsBorder.Visibility = Visibility.Visible;
    }
}
