using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DesktopHub.UI.Widgets;

public partial class DocQuickOpenWidget : System.Windows.Controls.UserControl
{
    private readonly DocOpenService _docService;
    private readonly Dictionary<Discipline, Border> _disciplineButtons = new();
    private Dictionary<string, string> _installedRevitVersions = new(); // version -> exe path
    private string? _matchedRevitExe; // exe path matching the project's Revit version

    public DocQuickOpenWidget(DocOpenService docService)
    {
        InitializeComponent();
        _docService = docService;

        _docService.ProjectChanged += (s, e) => Dispatcher.Invoke(RenderAll);
        _docService.ScanningChanged += (s, scanning) => Dispatcher.Invoke(() =>
        {
            LoadingIndicator.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
        });
        _docService.ConfigChanged += (s, e) => Dispatcher.Invoke(RenderAll);

        Loaded += async (s, e) =>
        {
            // Map discipline buttons
            _disciplineButtons[Discipline.Electrical] = BtnElectrical;
            _disciplineButtons[Discipline.Mechanical] = BtnMechanical;
            _disciplineButtons[Discipline.Plumbing] = BtnPlumbing;

            DetectInstalledRevitVersions();
            await _docService.InitializeAsync();
            RenderAll();
        };
    }

    /// <summary>
    /// Called by SearchOverlay when user clicks a project in search results
    /// </summary>
    public async Task SetProjectAsync(string projectPath, string? projectName = null)
    {
        await _docService.SetProjectAsync(projectPath, projectName);
    }

    private void RenderAll()
    {
        var info = _docService.ProjectInfo;

        if (info == null)
        {
            // No project selected
            ProjectNameText.Text = "Click a project in search to begin";
            ProjectNameText.Foreground = (WpfBrush)FindResource("TextSecondaryBrush");
            ProjectTypeBadge.Visibility = Visibility.Collapsed;
            DisciplinePanel.Visibility = Visibility.Collapsed;
            RevitInfoPanel.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Collapsed;
            FileScrollViewer.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            EmptyIcon.Text = "📐";
            EmptyTitle.Text = "No project selected";
            EmptyHint.Text = "Click a project in the search results to view its drawings";
            CountText.Text = "";
            RecentSection.Visibility = Visibility.Collapsed;
            return;
        }

        // Project name
        ProjectNameText.Text = info.ProjectName;
        ProjectNameText.Foreground = (WpfBrush)FindResource("TextBrush");

        // Type badge
        ProjectTypeBadge.Visibility = Visibility.Visible;
        switch (info.Type)
        {
            case ProjectType.Cad:
                ProjectTypeBadgeText.Text = "CAD";
                ProjectTypeBadgeText.Foreground = (WpfBrush)FindResource("CadBrush");
                break;
            case ProjectType.Revit:
                ProjectTypeBadgeText.Text = info.IsHybrid ? "HYBRID" : "REVIT";
                ProjectTypeBadgeText.Foreground = (WpfBrush)FindResource("RevitBrush");
                break;
            default:
                ProjectTypeBadgeText.Text = "UNKNOWN";
                ProjectTypeBadgeText.Foreground = (WpfBrush)FindResource("TextSecondaryBrush");
                break;
        }

        // Show discipline selector for CAD and hybrid projects
        bool showDisciplines = info.AvailableDisciplines.Count > 0;
        DisciplinePanel.Visibility = showDisciplines ? Visibility.Visible : Visibility.Collapsed;

        if (showDisciplines)
        {
            UpdateDisciplineButtons(info);
        }

        // Revit info panel + launch button
        if (info.Revit != null)
        {
            RevitInfoPanel.Visibility = Visibility.Visible;
            RenderRevitInfo(info.Revit);
            RenderRevitLaunchPanel(info.Revit);
        }
        else
        {
            RevitInfoPanel.Visibility = Visibility.Collapsed;
            RevitLaunchPanel.Visibility = Visibility.Collapsed;
        }

        // Files
        RenderFiles();
        RenderRecentFiles();
    }

    private void UpdateDisciplineButtons(ProjectFileInfo info)
    {
        var selected = _docService.SelectedDiscipline;
        var activeBg = new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0x4F, 0xC3, 0xF7));
        var inactiveBg = new WpfSolidColorBrush(WpfColor.FromArgb(0x10, 0xF5, 0xF7, 0xFA));
        var disabledBg = new WpfSolidColorBrush(WpfColor.FromArgb(0x08, 0xF5, 0xF7, 0xFA));

        foreach (var (discipline, btn) in _disciplineButtons)
        {
            bool available = info.AvailableDisciplines.Contains(discipline);
            bool isSelected = discipline == selected;

            btn.Background = isSelected ? activeBg : (available ? inactiveBg : disabledBg);
            btn.Opacity = available ? 1.0 : 0.4;
            btn.IsHitTestVisible = available;

            // Update text foreground
            var textBlock = btn.Child as TextBlock;
            if (textBlock != null)
            {
                textBlock.Foreground = isSelected
                    ? (WpfBrush)FindResource("AccentBrush")
                    : (WpfBrush)FindResource("TextSecondaryBrush");
            }
        }
    }

    private void RenderRevitInfo(RevitInfo revit)
    {
        // Version
        if (!string.IsNullOrEmpty(revit.RevitVersion))
        {
            RevitVersionText.Text = $"Version: Revit {revit.RevitVersion}";
            RevitVersionText.Visibility = Visibility.Visible;
        }
        else
        {
            RevitVersionText.Text = "Version: Unknown";
            RevitVersionText.Visibility = Visibility.Visible;
        }

        // Cloud status
        if (revit.IsCloudProject)
        {
            RevitCloudText.Text = "☁ Cloud Model (BIM 360 / ACC)";
            RevitCloudText.Foreground = (WpfBrush)FindResource("CloudBrush");
        }
        else
        {
            RevitCloudText.Text = "💾 Local Model";
            RevitCloudText.Foreground = (WpfBrush)FindResource("TextSecondaryBrush");
        }
        RevitCloudText.Visibility = Visibility.Visible;

        // CRevit
        if (revit.IsCRevit)
        {
            RevitCRevitText.Text = "CRevit: Yes";
            RevitCRevitText.Visibility = Visibility.Visible;
        }
        else
        {
            RevitCRevitText.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderFiles()
    {
        FileListPanel.Children.Clear();

        var docs = _docService.CurrentFiles;
        var info = _docService.ProjectInfo;

        if (info == null)
        {
            FileScrollViewer.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Collapsed;
            return;
        }

        // Hide search for Revit projects — the launch pills replace it
        bool isRevitProject = info.Revit != null;
        SearchPanel.Visibility = isRevitProject ? Visibility.Collapsed : Visibility.Visible;

        if (docs.Count == 0)
        {
            FileScrollViewer.Visibility = Visibility.Collapsed;
            CountText.Text = "";

            // Revit projects use the launch pills — no empty state needed
            if (isRevitProject)
            {
                EmptyState.Visibility = Visibility.Collapsed;
                return;
            }

            EmptyState.Visibility = Visibility.Visible;

            if (info.Type == ProjectType.Revit && info.Revit != null && info.Revit.IsCloudProject)
            {
                EmptyIcon.Text = "☁";
                EmptyTitle.Text = "Cloud Revit Model";
                EmptyHint.Text = "This project uses a cloud model — open via Revit Desktop Connector";
            }
            else if (info.Type == ProjectType.Unknown)
            {
                EmptyIcon.Text = "❓";
                EmptyTitle.Text = "No drawings found";
                EmptyHint.Text = "No Electrical/Mechanical/Plumbing or Revit File folders detected";
            }
            else
            {
                EmptyIcon.Text = "📂";
                EmptyTitle.Text = "No files in this folder";
                EmptyHint.Text = "Try switching disciplines or rescanning";
            }
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        FileScrollViewer.Visibility = Visibility.Visible;
        CountText.Text = $"{docs.Count} file{(docs.Count == 1 ? "" : "s")}";

        foreach (var doc in docs)
        {
            FileListPanel.Children.Add(CreateFileRow(doc));
        }
    }

    private Border CreateFileRow(DocumentItem doc)
    {
        var isCompact = _docService.Config.CompactMode;
        var row = new Border
        {
            Background = WpfBrushes.Transparent,
            CornerRadius = new CornerRadius(isCompact ? 4 : 6),
            Padding = isCompact ? new Thickness(6, 3, 6, 3) : new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, isCompact ? 1 : 2),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = doc.Path
        };

        row.MouseEnter += (s, e) => row.Background = (WpfBrush)FindResource("HoverBrush");
        row.MouseLeave += (s, e) => row.Background = WpfBrushes.Transparent;
        row.MouseLeftButtonDown += async (s, e) =>
        {
            if (e.ClickCount == 2)
                await OpenFileAsync(doc.Path);
        };
        row.MouseRightButtonDown += (s, e) => ShowFileContextMenu(doc, row);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Icon
        var icon = new TextBlock
        {
            Text = GetFileIcon(doc.Extension),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        // Name + details
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var displayName = _docService.Config.ShowFileExtension
            ? doc.FileName
            : System.IO.Path.GetFileNameWithoutExtension(doc.FileName);
        var nameBlock = new TextBlock
        {
            Text = displayName,
            FontSize = 12,
            Foreground = (WpfBrush)FindResource("TextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        infoStack.Children.Add(nameBlock);

        var detailParts = new List<string>();
        if (_docService.Config.ShowFileSize) detailParts.Add(doc.SizeDisplay);
        if (_docService.Config.ShowDateModified) detailParts.Add(doc.LastModified.ToString("MMM dd, yyyy"));

        if (detailParts.Count > 0)
        {
            infoStack.Children.Add(new TextBlock
            {
                Text = string.Join("  ·  ", detailParts),
                FontSize = 10,
                Foreground = (WpfBrush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 1, 0, 0)
            });
        }

        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        row.Child = grid;
        return row;
    }

    private void RenderRecentFiles()
    {
        RecentFilesPanel.Children.Clear();
        var recents = _docService.GetRecentFiles();

        if (recents.Count == 0 || _docService.Config.RecentFilesCount == 0)
        {
            RecentSection.Visibility = Visibility.Collapsed;
            return;
        }

        RecentSection.Visibility = Visibility.Visible;

        foreach (var filePath in recents.Take(5))
        {
            var fileName = System.IO.Path.GetFileName(filePath);
            var row = new Border
            {
                Background = WpfBrushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 3, 4, 3),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            row.MouseEnter += (s, e) => row.Background = (WpfBrush)FindResource("HoverBrush");
            row.MouseLeave += (s, e) => row.Background = WpfBrushes.Transparent;

            var path = filePath;
            row.MouseLeftButtonDown += async (s, e) =>
            {
                if (e.ClickCount == 2) await OpenFileAsync(path);
            };

            row.Child = new TextBlock
            {
                Text = fileName,
                FontSize = 11,
                Foreground = (WpfBrush)FindResource("TextSecondaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            RecentFilesPanel.Children.Add(row);
        }
    }

    private async Task OpenFileAsync(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            await _docService.RecordFileOpenAsync(filePath);
            RenderRecentFiles();
            StatusText.Text = $"Opened {System.IO.Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void ShowFileContextMenu(DocumentItem doc, Border row)
    {
        var menuItemStyle = CreateDarkMenuItemStyle();
        var menu = new ContextMenu
        {
            Background = new WpfSolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E)),
            BorderBrush = new WpfSolidColorBrush(WpfColor.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            Foreground = new WpfSolidColorBrush(WpfColor.FromRgb(0xE0, 0xE0, 0xE0))
        };
        menu.Resources[typeof(MenuItem)] = menuItemStyle;

        var openItem = new MenuItem { Header = "Open" };
        openItem.Click += async (s, e) => await OpenFileAsync(doc.Path);
        menu.Items.Add(openItem);

        var folderItem = new MenuItem { Header = "Open Folder" };
        folderItem.Click += (s, e) =>
        {
            try { Process.Start("explorer.exe", $"/select,\"{doc.Path}\""); }
            catch { }
        };
        menu.Items.Add(folderItem);

        var copyItem = new MenuItem { Header = "Copy Path" };
        copyItem.Click += (s, e) =>
        {
            try
            {
                System.Windows.Clipboard.SetText(doc.Path);
                StatusText.Text = "Path copied";
            }
            catch { }
        };
        menu.Items.Add(copyItem);

        row.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private Style CreateDarkMenuItemStyle()
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(MenuItem.ForegroundProperty, new WpfSolidColorBrush(WpfColor.FromRgb(0xE0, 0xE0, 0xE0))));
        style.Setters.Add(new Setter(MenuItem.BackgroundProperty, new WpfSolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E))));
        style.Setters.Add(new Setter(MenuItem.BorderBrushProperty, WpfBrushes.Transparent));
        style.Setters.Add(new Setter(MenuItem.PaddingProperty, new Thickness(8, 4, 8, 4)));

        var hover = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hover.Setters.Add(new Setter(MenuItem.BackgroundProperty, new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0x4F, 0xC3, 0xF7))));
        style.Triggers.Add(hover);

        return style;
    }

    // ---- Revit detection & launch ----

    private void DetectInstalledRevitVersions()
    {
        _installedRevitVersions.Clear();
        try
        {
            var autodeskDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk");
            if (!Directory.Exists(autodeskDir)) return;

            foreach (var dir in Directory.GetDirectories(autodeskDir, "Revit *"))
            {
                var dirName = Path.GetFileName(dir);
                // Extract version from "Revit 2024" or "Revit 2023"
                var parts = dirName.Split(' ', 2);
                if (parts.Length == 2 && parts[0].Equals("Revit", StringComparison.OrdinalIgnoreCase))
                {
                    var version = parts[1].Trim();
                    var exePath = Path.Combine(dir, "Revit.exe");
                    if (File.Exists(exePath))
                    {
                        _installedRevitVersions[version] = exePath;
                    }
                }
            }
        }
        catch { }
    }

    private void RenderRevitLaunchPanel(RevitInfo revit)
    {
        VersionPillRow.Children.Clear();
        VersionPillRow.ColumnDefinitions.Clear();

        if (string.IsNullOrEmpty(revit.RevitVersion))
        {
            if (_installedRevitVersions.Count > 0)
            {
                RevitLaunchPanel.Visibility = Visibility.Visible;
                RevitNotFoundPanel.Visibility = Visibility.Collapsed;
                BuildVersionPillGrid(null);
            }
            else
            {
                RevitLaunchPanel.Visibility = Visibility.Collapsed;
            }
            return;
        }

        RevitLaunchPanel.Visibility = Visibility.Visible;

        if (_installedRevitVersions.TryGetValue(revit.RevitVersion, out var exePath))
        {
            _matchedRevitExe = exePath;
            RevitNotFoundPanel.Visibility = Visibility.Collapsed;
            BuildVersionPillGrid(revit.RevitVersion);
        }
        else
        {
            _matchedRevitExe = null;
            RevitNotFoundPanel.Visibility = Visibility.Visible;
            var shortVer = revit.RevitVersion.Length >= 2 ? revit.RevitVersion.Substring(revit.RevitVersion.Length - 2) : revit.RevitVersion;
            RevitNotFoundText.Text = $"R{shortVer} not installed";
            BuildVersionPillGrid(null);
        }
    }

    private void BuildVersionPillGrid(string? primaryVersion)
    {
        VersionPillRow.Children.Clear();
        VersionPillRow.ColumnDefinitions.Clear();

        // Build ordered list: primary version first (if matched), then others descending
        var allVersions = new List<(string version, string exePath, bool isPrimary)>();

        if (primaryVersion != null && _installedRevitVersions.TryGetValue(primaryVersion, out var primaryExe))
        {
            allVersions.Add((primaryVersion, primaryExe, true));
        }

        var others = _installedRevitVersions
            .Where(kv => kv.Key != primaryVersion)
            .OrderByDescending(kv => kv.Key);
        foreach (var kv in others)
            allVersions.Add((kv.Key, kv.Value, false));

        if (allVersions.Count == 0) return;

        // Primary pill gets 2x width so "Launch R## ▶" isn't clipped
        for (int i = 0; i < allVersions.Count; i++)
        {
            var weight = allVersions[i].isPrimary ? 2 : 1;
            VersionPillRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
        }

        for (int i = 0; i < allVersions.Count; i++)
        {
            var (version, exePath, isPrimary) = allVersions[i];
            var path = exePath;
            var shortVer = version.Length >= 2 ? version.Substring(version.Length - 2) : version;
            var label = isPrimary ? $"Launch R{shortVer} ▶" : $"R{shortVer}";
            var defaultBg = isPrimary
                ? new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0xFF, 0x6B, 0x35))
                : new WpfSolidColorBrush(WpfColor.FromArgb(0x18, 0xFF, 0x6B, 0x35));

            var pill = new Border
            {
                Background = defaultBg,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 6, 6, 6),
                Margin = new Thickness(i == 0 ? 0 : 3, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"Launch Revit {version}"
            };

            var bg = defaultBg;
            pill.MouseEnter += (s, e) => pill.Background = new WpfSolidColorBrush(WpfColor.FromArgb(0x50, 0xFF, 0x6B, 0x35));
            pill.MouseLeave += (s, e) => pill.Background = bg;
            pill.MouseLeftButtonDown += (s, e) => LaunchRevit(path, version);

            pill.Child = new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = (WpfBrush)FindResource("RevitBrush"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(pill, i);
            VersionPillRow.Children.Add(pill);
        }
    }

    private void LaunchRevit(string exePath, string version)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            StatusText.Text = $"Launched Revit {version}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    // ---- Event handlers ----

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _docService.SetSearchQuery(SearchBox.Text);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await _docService.RescanAsync();
        StatusText.Text = "Rescanned";
    }

    private async void Discipline_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border btn && btn.Tag is string tag)
        {
            if (Enum.TryParse<Discipline>(tag, true, out var disc))
            {
                await _docService.SetDisciplineAsync(disc);
            }
        }
    }

    private static string GetFileIcon(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            "pdf" => "📄",
            "dwg" or "dxf" or "dgn" => "📐",
            "rvt" or "rfa" => "🏗",
            "dwf" or "dwfx" => "📋",
            "doc" or "docx" => "📝",
            "xls" or "xlsx" or "csv" => "📊",
            "ppt" or "pptx" => "📽",
            "txt" => "📃",
            "msg" or "eml" => "✉",
            "jpg" or "jpeg" or "png" or "bmp" or "tif" or "tiff" => "🖼",
            _ => "📄"
        };
    }
}
