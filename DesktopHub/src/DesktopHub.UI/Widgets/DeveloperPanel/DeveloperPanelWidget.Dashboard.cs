using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget
{
    // ════════════════════════════════════════════════════════════
    // SCRIPT TILES
    // ════════════════════════════════════════════════════════════

    private void BuildScriptTiles()
    {
        var tiles = new List<ScriptTile>
        {
            new("Dump DB",       "db",  "#42A5F5", () => RunScriptAsync("admin.ps1", "-Action", "db-dump")),
            new("Backup",        "bk",  "#66BB6A", () => RunScriptAsync("admin.ps1", "-Action", "db-backup")),
            new("List Devices",  "ls",  "#42A5F5", () => RunScriptAsync("admin.ps1", "-Action", "update-list")),
            new("Push Update",   "pu",  "#FFA726", () => RunPushUpdateAll()),
            new("Build",         "bl",  "#AB47BC", () => RunScriptAsync("admin.ps1", "-Action", "build")),
            new("Version",       "vr",  "#26C6DA", () => RunVersionUpdate()),
            new("List Tags",     "tg",  "#66BB6A", () => RunScriptAsync("admin.ps1", "-Action", "tags-list")),
            new("Auth Users",    "au",  "#FFA726", () => RunScriptAsync("cleanup-auth-users.ps1")),
            new("Console",       "ac",  "#42A5F5", () => RunScriptAsync("admin.ps1")),
            new("Decrypt Tags",  "dt",  "#66BB6A", () => RunScriptAsync("admin.ps1", "-Action", "tags-decrypt")),
            new("Metrics Reset", "mr",  "#EF5350", () => RunScriptAsync("admin.ps1", "-Action", "metrics-reset")),
            // Bug fix: call tag-manager.ps1 directly with skipServiceAccount to avoid parameter mismatch
            new("HMAC Secret",   "hm",  "#78909C", () => RunScriptWithOutputAsync("tag-manager.ps1", skipServiceAccount: true, "-Action", "show-secret")),
        };

        ScriptTileGrid.Children.Clear();
        foreach (var tile in tiles)
            ScriptTileGrid.Children.Add(CreateScriptTile(tile));
    }

    private UIElement CreateScriptTile(ScriptTile tile)
    {
        var accentColor = (Color)ColorConverter.ConvertFromString(tile.Color);
        var accentBrush = new SolidColorBrush(accentColor);
        var bgBrush = new SolidColorBrush(Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B));
        var borderBrush = new SolidColorBrush(Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B));

        var abbrevBlock = new TextBlock
        {
            Text = tile.Abbrev,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = accentBrush,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var labelBlock = new TextBlock
        {
            Text = tile.Label,
            FontSize = 9,
            Foreground = FindBrush("TextSecondaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0)
        };

        var statusDot = new System.Windows.Shapes.Ellipse
        {
            Width = 6, Height = 6,
            Fill = FindBrush("TextTertiaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };

        var innerGrid = new Grid();
        innerGrid.Children.Add(statusDot);
        var stack = new StackPanel();
        stack.Children.Add(abbrevBlock);
        stack.Children.Add(labelBlock);
        innerGrid.Children.Add(stack);

        var border = new Border
        {
            Background = bgBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 4, 4),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = innerGrid,
            ToolTip = tile.Label
        };

        var hoverBg = new SolidColorBrush(Color.FromArgb(50, accentColor.R, accentColor.G, accentColor.B));
        border.MouseEnter += (_, _) => border.Background = hoverBg;
        border.MouseLeave += (_, _) => border.Background = bgBrush;
        border.MouseLeftButtonDown += async (_, _) =>
        {
            statusDot.Fill = FindBrush("GreenBrush");
            try { await tile.Action(); }
            catch (Exception ex) { AppendOutput($"ERROR: {ex.Message}"); }
        };

        return border;
    }

    // ════════════════════════════════════════════════════════════
    // VERSION INFO
    // ════════════════════════════════════════════════════════════

    private async Task RefreshVersionInfoAsync()
    {
        if (!EnsureDevForAction("refresh version info")) return;
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            VersionInfoText.Text = "Firebase service unavailable.";
            return;
        }

        try
        {
            var node = await _firebaseService.GetNodeAsync("app_versions/desktophub");
            if (node == null)
            {
                VersionInfoText.Text = "No version data found at app_versions/desktophub.";
                return;
            }

            var latest = node.TryGetValue("latest_version", out var lv) ? lv?.ToString() : "n/a";
            var notes = node.TryGetValue("release_notes", out var rn) ? rn?.ToString() : "";
            var updatedAt = node.TryGetValue("updated_at", out var ua) ? ua?.ToString() : "n/a";
            var required = node.TryGetValue("required_update", out var ru) ? ru?.ToString() : "false";

            VersionInfoText.Text = $"Latest: {latest} | Required: {required} | Updated: {updatedAt}{Environment.NewLine}{notes}";
        }
        catch (Exception ex)
        {
            VersionInfoText.Text = $"Failed to load version info: {ex.Message}";
        }
    }

    private async Task RunPushUpdateAll()
    {
        if (!ConfirmDangerous("Push update to all outdated devices?")) return;
        await RunScriptAsync("admin.ps1", "-Action", "update-push-all");
    }

    private async Task RunVersionUpdate()
    {
        var version = DateTime.Now.ToString("yyyy.M.d", CultureInfo.InvariantCulture);
        await RunScriptAsync("admin.ps1", "-Action", "version-update", "-Version", version, "-ReleaseNotes", "Updated_from_Developer_Panel");
    }

    // Event handler
    private async void RefreshVersionInfo_Click(object sender, RoutedEventArgs e) => await RefreshVersionInfoAsync();
}
