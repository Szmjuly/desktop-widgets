using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget
{
    private sealed record LicenseRecord(
        string Key,
        string Plan,
        string Status,
        string AppId,
        int MaxDevices,
        string ExpiresAt,
        string Username,
        string CreatedAt);

    private readonly List<LicenseRecord> _licenses = new();

    private async Task RefreshLicensesAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
            return;

        try
        {
            LicensePlanBox.ItemsSource = new[] { "FREE", "PRO", "ENTERPRISE" };
            LicenseAppBox.ItemsSource = new[] { "desktophub" };
            if (LicensePlanBox.SelectedIndex < 0) LicensePlanBox.SelectedIndex = 0;
            if (LicenseAppBox.SelectedIndex < 0) LicenseAppBox.SelectedIndex = 0;
            if (string.IsNullOrWhiteSpace(LicenseKeyBox.Text))
                LicenseKeyBox.Text = GenerateLicenseKey();

            // Licenses now live at tenants/{tid}/licenses/{appId}/{licenseKey}.
            // Flatten the two-level node (appId -> licenseKey -> record) into
            // the local list so the existing renderer keeps working.
            var node = await _firebaseService.GetNodeAsync(_firebaseService.TenantPath("licenses"));
            _licenses.Clear();

            if (node != null)
            {
                foreach (var appKvp in node)
                {
                    if (appKvp.Value is not Dictionary<string, object> appLicenses) continue;
                    foreach (var kvp in appLicenses)
                    {
                        if (kvp.Value is not Dictionary<string, object> data) continue;
                        _licenses.Add(new LicenseRecord(
                            kvp.Key,
                            data.TryGetValue("plan", out var p) ? p?.ToString() ?? "FREE" : "FREE",
                            data.TryGetValue("status", out var s) ? s?.ToString() ?? "active" : "active",
                            data.TryGetValue("app_id", out var a) ? a?.ToString() ?? appKvp.Key : appKvp.Key,
                            data.TryGetValue("max_devices", out var m) && int.TryParse(m?.ToString(), out var md) ? md : 1,
                            data.TryGetValue("expires_at", out var e) ? e?.ToString() ?? "never" : "never",
                            data.TryGetValue("user_id", out var u) ? u?.ToString() ?? "" : "",
                            data.TryGetValue("created_at", out var c) ? c?.ToString() ?? "" : ""
                        ));
                    }
                }
            }

            RenderLicenseList();
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR loading licenses: {ex.Message}");
        }
    }

    private void RenderLicenseList()
    {
        LicenseListPanel.Children.Clear();
        foreach (var license in _licenses.OrderByDescending(l => l.CreatedAt).ThenBy(l => l.Key))
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();
            left.Children.Add(new TextBlock
            {
                Text = license.Key,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = FindBrush("TextPrimaryBrush")
            });
            left.Children.Add(new TextBlock
            {
                Text = $"{license.AppId} | {license.Plan} | {license.Status}",
                FontSize = 10,
                Foreground = FindBrush("TextSecondaryBrush"),
                Margin = new Thickness(0, 2, 0, 0)
            });
            left.Children.Add(new TextBlock
            {
                Text = $"Max devices: {license.MaxDevices} | Expires: {(string.IsNullOrWhiteSpace(license.ExpiresAt) ? "never" : license.ExpiresAt)}",
                FontSize = 9,
                Foreground = FindBrush("TextTertiaryBrush"),
                Margin = new Thickness(0, 2, 0, 0)
            });

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var revoke = new System.Windows.Controls.Button { Content = "Revoke", Style = (Style)FindResource("ActionBtn"), Margin = new Thickness(0, 0, 6, 0) };
            revoke.Click += async (_, _) => await RevokeLicenseAsync(license.Key, license.AppId);
            buttons.Children.Add(revoke);

            var delete = new System.Windows.Controls.Button { Content = "Delete", Style = (Style)FindResource("DangerBtn"), Margin = new Thickness(0) };
            delete.Click += async (_, _) => await DeleteLicenseAsync(license.Key, license.AppId);
            buttons.Children.Add(delete);

            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);

            LicenseListPanel.Children.Add(new Border
            {
                Background = FindBrush("FaintOverlayBrush"),
                BorderBrush = FindBrush("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Child = grid
            });
        }
    }

    private string GenerateLicenseKey()
    {
        var plan = (LicensePlanBox.SelectedItem?.ToString() ?? "FREE").ToUpperInvariant();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"{plan}-{suffix}";
    }

    private async Task RevokeLicenseAsync(string key, string appId)
    {
        if (!await ConfirmDangerousAsync($"Revoke license '{key}'?"))
            return;
        if (_firebaseService == null) return;
        var ok = await _firebaseService.SetNodeAsync(
            _firebaseService.TenantPath($"licenses/{appId}/{key}/status"), "revoked");
        AppendOutput(ok ? $"Revoked {key}" : $"Failed to revoke {key}");
        await RefreshLicensesAsync();
    }

    private async Task DeleteLicenseAsync(string key, string appId)
    {
        if (!await ConfirmDangerousAsync($"Delete license '{key}'? This cannot be undone."))
            return;
        if (_firebaseService == null) return;
        var ok = await _firebaseService.DeleteNodeAsync(
            _firebaseService.TenantPath($"licenses/{appId}/{key}"));
        AppendOutput(ok ? $"Deleted {key}" : $"Failed to delete {key}");
        await RefreshLicensesAsync();
    }

    private async void CreateLicense_Click(object sender, RoutedEventArgs e)
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
            return;

        var key = (LicenseKeyBox.Text ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(key))
        {
            AppendOutput("License key is required.");
            return;
        }

        var plan = (LicensePlanBox.SelectedItem?.ToString() ?? "FREE").ToUpperInvariant();
        var appId = LicenseAppBox.SelectedItem?.ToString() ?? "desktophub";
        if (!int.TryParse(LicenseMaxDevicesBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxDevices))
            maxDevices = 1;

        var expiresAt = (LicenseExpiresAtBox.Text ?? "").Trim();
        var payload = new Dictionary<string, object>
        {
            ["plan"] = plan,
            ["status"] = "active",
            ["app_id"] = appId,
            ["max_devices"] = maxDevices,
            ["expires_at"] = string.IsNullOrWhiteSpace(expiresAt) ? "never" : expiresAt,
            ["created_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            ["user_id"] = _firebaseService.Auth.UserId ?? "unknown",
        };

        var ok = await _firebaseService.SetNodeAsync(
            _firebaseService.TenantPath($"licenses/{appId}/{key}"), payload);
        AppendOutput(ok ? $"Created license {key}" : $"Failed to create license {key}");
        if (ok)
        {
            LicenseKeyBox.Text = GenerateLicenseKey();
            LicenseExpiresAtBox.Text = "";
            await RefreshLicensesAsync();
        }
    }

    private async void RefreshLicenses_Click(object sender, RoutedEventArgs e)
    {
        await RefreshLicensesAsync();
    }
}
