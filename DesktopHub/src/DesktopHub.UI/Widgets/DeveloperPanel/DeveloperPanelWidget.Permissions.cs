using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget
{
    // ════════════════════════════════════════════════════════════
    // USERNAME AUTOCOMPLETE
    // ════════════════════════════════════════════════════════════

    private void RoleUsernameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSuggestion) return;

        var text = RoleUsernameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text) || _knownUsernames.Count == 0)
        {
            UserSuggestionPopup.IsOpen = false;
            return;
        }

        var matches = _knownUsernames
            .Where(u => u.StartsWith(text, StringComparison.OrdinalIgnoreCase) && !u.Equals(text, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();

        if (matches.Count == 0)
        {
            UserSuggestionPopup.IsOpen = false;
            return;
        }

        UserSuggestionList.Items.Clear();
        foreach (var match in matches)
            UserSuggestionList.Items.Add(match);

        UserSuggestionPopup.IsOpen = true;
    }

    private void UserSuggestion_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (UserSuggestionList.SelectedItem is string selected)
        {
            _suppressSuggestion = true;
            RoleUsernameBox.Text = selected;
            RoleUsernameBox.CaretIndex = selected.Length;
            _suppressSuggestion = false;
            UserSuggestionPopup.IsOpen = false;
            UserSuggestionList.SelectedItem = null;
        }
    }

    private void RoleUsernameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Small delay to allow click on suggestion to register
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            if (!UserSuggestionList.IsMouseOver)
                UserSuggestionPopup.IsOpen = false;
        });
    }

    // ════════════════════════════════════════════════════════════
    // INLINE RESULTS DISPLAY
    // ════════════════════════════════════════════════════════════

    private void ShowPermResults(string stdout, string roleType)
    {
        PermResultsPanel.Children.Clear();

        var lines = stdout
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("---") && !l.StartsWith("==="))
            .ToList();

        if (lines.Count == 0)
        {
            PermResultsBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var (bgKey, fgKey) = roleType switch
        {
            "admin" => ("OrangeBackgroundBrush", "OrangeBrush"),
            "editor" => ("GreenBackgroundBrush", "GreenBrush"),
            "dev" => ("BlueBackgroundBrush", "BlueBrush"),
            _ => ("SurfaceBrush", "TextPrimaryBrush")
        };

        foreach (var line in lines)
        {
            var chip = new Border
            {
                Background = FindBrush(bgKey),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 4),
                Child = new TextBlock
                {
                    Text = line,
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = FindBrush(fgKey)
                }
            };
            PermResultsPanel.Children.Add(chip);
        }

        PermResultsBorder.Visibility = Visibility.Visible;
    }

    // ════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ════════════════════════════════════════════════════════════

    private async void ListAdmins_Click(object sender, RoutedEventArgs e)
    {
        var (stdout, _, exitCode) = await RunScriptWithOutputAsync("manage-admin.ps1", false, "-Action", "list");
        if (exitCode == 0) ShowPermResults(stdout, "admin");
    }

    private async void ListEditors_Click(object sender, RoutedEventArgs e)
    {
        var (stdout, _, exitCode) = await RunScriptWithOutputAsync("manage-cheatsheet-editors.ps1", false, "-Action", "list");
        if (exitCode == 0) ShowPermResults(stdout, "editor");
    }

    private async void ListDevs_Click(object sender, RoutedEventArgs e)
    {
        var (stdout, _, exitCode) = await RunScriptWithOutputAsync("manage-dev.ps1", false, "-Action", "list");
        if (exitCode == 0) ShowPermResults(stdout, "dev");
    }

    private async void AddAdmin_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-admin.ps1", "add");
    private async void RemoveAdmin_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-admin.ps1", "remove", confirmDangerous: true);
    private async void AddEditor_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-cheatsheet-editors.ps1", "add");
    private async void RemoveEditor_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-cheatsheet-editors.ps1", "remove", confirmDangerous: true);
    private async void AddDev_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-dev.ps1", "add");
    private async void RemoveDev_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-dev.ps1", "remove", confirmDangerous: true);
}
