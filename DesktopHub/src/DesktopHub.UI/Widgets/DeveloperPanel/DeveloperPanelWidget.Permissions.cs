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

    // Cached snapshot from the last listTenantUsers call. Populated by
    // RefreshPermissionDirectoryAsync and read by RefreshSelectedUserPermissionsAsync.
    private List<TenantUser> _tenantUsers = new();

    private sealed class TenantUser
    {
        public string UserId { get; set; } = "";
        public string Username { get; set; } = "";
        public bool IsAdmin { get; set; }
        public bool IsDev { get; set; }
        public bool IsEditor { get; set; }
    }

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
            // Cloud Function decrypts the user_directory and returns
            // admin/dev/editor flags in one round trip. Client never sees
            // the ciphertext directly.
            var result = await _firebaseService.Auth.CallFunctionAsync(
                "listTenantUsers", new { });
            if (result == null)
            {
                SetUnavailable();
                AppendOutput("ERROR: listTenantUsers call failed (are you admin/dev?).");
                return;
            }

            var list = new List<TenantUser>();
            var root = result.Value;
            if (root.TryGetProperty("users", out var usersEl) &&
                usersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var u in usersEl.EnumerateArray())
                {
                    list.Add(new TenantUser
                    {
                        UserId = u.TryGetProperty("userId", out var uid) ? uid.GetString() ?? "" : "",
                        Username = u.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "",
                        IsAdmin = u.TryGetProperty("isAdmin", out var ia) && ia.ValueKind == JsonValueKind.True,
                        IsDev = u.TryGetProperty("isDev", out var id) && id.ValueKind == JsonValueKind.True,
                        IsEditor = u.TryGetProperty("isEditor", out var ie) && ie.ValueKind == JsonValueKind.True,
                    });
                }
            }
            _tenantUsers = list;

            PermDirAdminLine.Text = $"Admins: {list.Count(u => u.IsAdmin)}";
            PermDirEditorLine.Text = $"Editors: {list.Count(u => u.IsEditor)} (cheat_sheet_editors)";
            PermDirDevLine.Text = $"Devs: {list.Count(u => u.IsDev)}";
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
            // Use the cached tenant user list populated by RefreshPermissionDirectoryAsync.
            // If empty (first call), fetch now.
            if (_tenantUsers.Count == 0)
                await RefreshPermissionDirectoryAsync();

            var match = _tenantUsers.FirstOrDefault(
                u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                PermSelectedUserBadges.Children.Add(CreatePermHint(
                    "No directory entry for this user yet — they must sign in once to appear."));
                return;
            }

            if (match.IsAdmin)
                PermSelectedUserBadges.Children.Add(CreatePermissionRoleBadge("ADMIN", "OrangeBackgroundBrush", "OrangeBrush"));
            if (match.IsEditor)
                PermSelectedUserBadges.Children.Add(CreatePermissionRoleBadge("EDITOR", "GreenBackgroundBrush", "GreenBrush"));
            if (match.IsDev)
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
        var username = ResolveUsername();
        if (string.IsNullOrWhiteSpace(username)) { AppendOutput("Enter a username first."); return; }
        await SetRoleAsync("admin_users", username, true);
        await AfterRoleMutationAsync();
    }

    private async void RemoveAdmin_Click(object sender, RoutedEventArgs e)
    {
        var username = ResolveUsername();
        if (string.IsNullOrWhiteSpace(username)) { AppendOutput("Enter a username first."); return; }
        if (!await ConfirmDangerousAsync($"Remove admin role from '{username}'?")) return;
        await SetRoleAsync("admin_users", username, false);
        await AfterRoleMutationAsync();
    }

    private async void AddEditor_Click(object sender, RoutedEventArgs e)
    {
        var username = ResolveUsername();
        if (string.IsNullOrWhiteSpace(username)) { AppendOutput("Enter a username first."); return; }
        await SetRoleAsync("cheat_sheet_editors", username, true);
        await AfterRoleMutationAsync();
    }

    private async void RemoveEditor_Click(object sender, RoutedEventArgs e)
    {
        var username = ResolveUsername();
        if (string.IsNullOrWhiteSpace(username)) { AppendOutput("Enter a username first."); return; }
        if (!await ConfirmDangerousAsync($"Remove editor role from '{username}'?")) return;
        await SetRoleAsync("cheat_sheet_editors", username, false);
        await AfterRoleMutationAsync();
    }

    private async void AddDev_Click(object sender, RoutedEventArgs e)
    {
        var username = ResolveUsername();
        if (string.IsNullOrWhiteSpace(username)) { AppendOutput("Enter a username first."); return; }
        await SetRoleAsync("dev_users", username, true);
        await AfterRoleMutationAsync();
    }

    private async void RemoveDev_Click(object sender, RoutedEventArgs e)
    {
        var username = ResolveUsername();
        if (string.IsNullOrWhiteSpace(username)) { AppendOutput("Enter a username first."); return; }
        if (!await ConfirmDangerousAsync($"Remove dev role from '{username}'?")) return;
        await SetRoleAsync("dev_users", username, false);
        await AfterRoleMutationAsync();
    }
}
