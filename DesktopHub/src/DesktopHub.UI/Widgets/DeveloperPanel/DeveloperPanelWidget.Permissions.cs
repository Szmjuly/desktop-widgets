using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget
{
    private bool _permTabEventsWired;
    private bool _populatingRoleUsernameDropdown;

    // ════════════════════════════════════════════════════════════
    // USERNAME DROPDOWN
    // ════════════════════════════════════════════════════════════

    private void PopulateUsernameDropdown()
    {
        if (RoleUsernameBox == null) return;
        _populatingRoleUsernameDropdown = true;
        try
        {
            var current = RoleUsernameBox.Text;
            RoleUsernameBox.ItemsSource = _knownUsernames;
            RoleUsernameBox.Text = current;
        }
        finally
        {
            _populatingRoleUsernameDropdown = false;
        }
    }

    internal void WirePermissionTabEventsOnce()
    {
        if (_permTabEventsWired || RoleUsernameBox == null)
            return;
        _permTabEventsWired = true;

        // Do not refresh on SelectionChanged — for an editable ComboBox, Text often updates after
        // selection commits; refreshing there shows the previous user. DropDownClosed is the
        // reliable point after a list pick.
        RoleUsernameBox.DropDownClosed += (_, _) =>
        {
            if (_populatingRoleUsernameDropdown) return;
            SyncRoleUsernameTextFromListSelection();
            _ = Dispatcher.InvokeAsync(
                async () => await RefreshSelectedUserPermissionsAsync(),
                DispatcherPriority.Input);
        };

        RoleUsernameBox.LostKeyboardFocus += async (_, _) =>
        {
            if (_populatingRoleUsernameDropdown) return;
            await RefreshSelectedUserPermissionsAsync();
        };
    }

    /// <summary>
    /// After choosing from the dropdown, commit SelectedItem into Text so the rest of the panel
    /// (and ResolveUsername) see the same value.
    /// </summary>
    private void SyncRoleUsernameTextFromListSelection()
    {
        if (RoleUsernameBox == null || _populatingRoleUsernameDropdown) return;
        if (RoleUsernameBox.SelectedItem is string sel && !string.IsNullOrWhiteSpace(sel))
            RoleUsernameBox.Text = NormalizeUsername(sel);
    }

    internal async Task RefreshPermissionTabStateAsync()
    {
        await RefreshPermissionDirectoryAsync();
        await RefreshSelectedUserPermissionsAsync();
    }

    // ════════════════════════════════════════════════════════════
    // DIRECTORY COUNTS (Firebase)
    // ════════════════════════════════════════════════════════════

    private async Task RefreshPermissionDirectoryAsync()
    {
        if (PermDirAdminLine == null || PermDirEditorLine == null || PermDirDevLine == null)
            return;

        void SetUnavailable()
        {
            PermDirAdminLine.Text = "Admins: —";
            PermDirEditorLine.Text = "Editors: —";
            PermDirDevLine.Text = "Devs: —";
        }

        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            SetUnavailable();
            return;
        }

        try
        {
            var admins = await _firebaseService.GetNodeAsync("admin_users");
            var editors = await _firebaseService.GetNodeAsync("cheat_sheet_editors");
            var devs = await _firebaseService.GetNodeAsync("dev_users");

            var ac = CountActiveRoleMembers(admins);
            var ec = CountActiveRoleMembers(editors);
            var dc = CountActiveRoleMembers(devs);

            PermDirAdminLine.Text = $"Admins: {ac}";
            PermDirEditorLine.Text = $"Editors: {ec} (cheat_sheet_editors)";
            PermDirDevLine.Text = $"Devs: {dc}";
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR loading role directory: {ex.Message}");
            SetUnavailable();
        }
    }

    private static int CountActiveRoleMembers(Dictionary<string, object>? node)
    {
        if (node == null || node.Count == 0) return 0;
        return node.Values.Count(PermissionValueIsTrue);
    }

    private static bool PermissionValueIsTrue(object? v)
    {
        if (v == null) return false;
        if (v is bool b) return b;
        if (v is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => je.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true,
                JsonValueKind.Number => je.TryGetInt64(out var n) && n != 0,
                _ => false
            };
        }

        var s = v.ToString();
        if (string.IsNullOrWhiteSpace(s)) return false;
        return s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1";
    }

    private static bool UserHasRole(Dictionary<string, object>? roleRoot, string username)
    {
        if (roleRoot == null || string.IsNullOrWhiteSpace(username)) return false;
        foreach (var kvp in roleRoot)
        {
            if (!kvp.Key.Equals(username, StringComparison.OrdinalIgnoreCase)) continue;
            return PermissionValueIsTrue(kvp.Value);
        }

        return false;
    }

    // ════════════════════════════════════════════════════════════
    // SELECTED USER — ROLE BADGES
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Editable ComboBox can leave <see cref="ComboBox.SelectedItem"/> out of sync with <see cref="ComboBox.Text"/>
    /// while typing. Prefer Text when it disagrees with the list selection; otherwise use the list row.
    /// </summary>
    private string GetEffectivePermissionsUsername()
    {
        if (RoleUsernameBox == null) return string.Empty;
        var text = NormalizeUsername(RoleUsernameBox.Text ?? string.Empty);

        if (RoleUsernameBox.SelectedIndex >= 0 && RoleUsernameBox.SelectedItem is string sel)
        {
            var normSel = NormalizeUsername(sel);
            if (string.IsNullOrEmpty(text))
                return normSel;
            if (string.Equals(text, normSel, StringComparison.OrdinalIgnoreCase))
                return normSel;
        }

        return text;
    }

    private async Task RefreshSelectedUserPermissionsAsync()
    {
        if (PermSelectedUserBadges == null || PermSelectedUserName == null)
            return;

        PermSelectedUserBadges.Children.Clear();

        // Prefer list selection when it matches the editable text box (dropdown pick);
        // otherwise use Text for free-typed usernames.
        var username = GetEffectivePermissionsUsername();

        if (string.IsNullOrWhiteSpace(username))
        {
            PermSelectedUserName.Text = "—";
            PermSelectedUserBadges.Children.Add(CreatePermHint("Enter or choose a username."));
            return;
        }

        PermSelectedUserName.Text = username;

        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            PermSelectedUserBadges.Children.Add(CreatePermHint("Firebase unavailable."));
            return;
        }

        try
        {
            var admins = await _firebaseService.GetNodeAsync("admin_users");
            var editors = await _firebaseService.GetNodeAsync("cheat_sheet_editors");
            var devs = await _firebaseService.GetNodeAsync("dev_users");

            var isAdmin = UserHasRole(admins, username);
            var isEditor = UserHasRole(editors, username);
            var isDev = UserHasRole(devs, username);

            if (isAdmin)
                PermSelectedUserBadges.Children.Add(CreatePermissionRoleBadge("ADMIN", "OrangeBackgroundBrush", "OrangeBrush"));
            if (isEditor)
                PermSelectedUserBadges.Children.Add(CreatePermissionRoleBadge("EDITOR", "GreenBackgroundBrush", "GreenBrush"));
            if (isDev)
                PermSelectedUserBadges.Children.Add(CreatePermissionRoleBadge("DEV", "BlueBackgroundBrush", "BlueBrush"));

            if (PermSelectedUserBadges.Children.Count == 0)
                PermSelectedUserBadges.Children.Add(CreatePermHint("No ADMIN, EDITOR, or DEV flags for this user."));
        }
        catch (Exception ex)
        {
            PermSelectedUserBadges.Children.Add(CreatePermHint($"Could not load roles: {ex.Message}"));
        }
    }

    private UIElement CreatePermHint(string text) =>
        new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = FindBrush("TextTertiaryBrush"),
            TextWrapping = TextWrapping.Wrap,
        };

    private Border CreatePermissionRoleBadge(string label, string bgKey, string fgKey) =>
        new()
        {
            Background = FindBrush(bgKey),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 6, 4),
            Padding = new Thickness(8, 3, 8, 3),
            Child = new TextBlock
            {
                Text = label,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = FindBrush(fgKey),
            },
        };

    // ════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ════════════════════════════════════════════════════════════

    private async void RefreshPermDirectory_Click(object sender, RoutedEventArgs e) =>
        await RefreshPermissionDirectoryAsync();

    private async Task AfterRoleMutationAsync()
    {
        await RefreshPermissionDirectoryAsync();
        await RefreshSelectedUserPermissionsAsync();
    }

    private async void AddAdmin_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRoleActionAsync("manage-admin.ps1", "add");
        await AfterRoleMutationAsync();
    }

    private async void RemoveAdmin_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRoleActionAsync("manage-admin.ps1", "remove", confirmDangerous: true);
        await AfterRoleMutationAsync();
    }

    private async void AddEditor_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRoleActionAsync("manage-cheatsheet-editors.ps1", "add");
        await AfterRoleMutationAsync();
    }

    private async void RemoveEditor_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRoleActionAsync("manage-cheatsheet-editors.ps1", "remove", confirmDangerous: true);
        await AfterRoleMutationAsync();
    }

    private async void AddDev_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRoleActionAsync("manage-dev.ps1", "add");
        await AfterRoleMutationAsync();
    }

    private async void RemoveDev_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteRoleActionAsync("manage-dev.ps1", "remove", confirmDangerous: true);
        await AfterRoleMutationAsync();
    }
}
