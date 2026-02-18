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
            ProjectNameText.Text = "Click a project in search results to begin";
            ProjectNameText.Foreground = (WpfBrush)FindResource("TextSecondaryBrush");
            ProjectTypeBadge.Visibility = Visibility.Collapsed;
            DisciplinePanel.Visibility = Visibility.Collapsed;
            RevitInfoPanel.Visibility = Visibility.Collapsed;
            RevitLaunchPanel.Visibility = Visibility.Collapsed;
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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

        // Inline copy button
        var copyBtn = new Border
        {
            Background = WpfBrushes.Transparent,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Copy path",
            Opacity = 0.6
        };
        copyBtn.Child = new TextBlock
        {
            Text = "📋",
            FontSize = 11,
            Foreground = WpfBrushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        copyBtn.MouseEnter += (s, e) => { copyBtn.Background = (WpfBrush)FindResource("HoverBrush"); copyBtn.Opacity = 1.0; };
        copyBtn.MouseLeave += (s, e) => { copyBtn.Background = WpfBrushes.Transparent; copyBtn.Opacity = 0.6; };
        var docPath = doc.Path;
        copyBtn.MouseLeftButtonDown += (s, e) =>
        {
            e.Handled = true;
            CopyToClipboard(docPath);
        };
        Grid.SetColumn(copyBtn, 2);
        grid.Children.Add(copyBtn);

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

            // Context menu for recent files
            var recentPath = filePath;
            row.MouseRightButtonDown += (s, e) => ShowRecentFileContextMenu(recentPath, row);

            var recentGrid = new Grid();
            recentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            recentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = fileName,
                FontSize = 11,
                Foreground = (WpfBrush)FindResource("TextSecondaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 0);
            recentGrid.Children.Add(nameText);

            // Inline copy button for recent files
            var recentCopyBtn = new Border
            {
                Background = WpfBrushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 1, 4, 1),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Copy path",
                Opacity = 0.6
            };
            recentCopyBtn.Child = new TextBlock
            {
                Text = "📋",
                FontSize = 10,
                Foreground = WpfBrushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            recentCopyBtn.MouseEnter += (s, e) => { recentCopyBtn.Background = (WpfBrush)FindResource("HoverBrush"); recentCopyBtn.Opacity = 1.0; };
            recentCopyBtn.MouseLeave += (s, e) => { recentCopyBtn.Background = WpfBrushes.Transparent; recentCopyBtn.Opacity = 0.6; };
            var copyPath = filePath;
            recentCopyBtn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                CopyToClipboard(copyPath);
            };
            Grid.SetColumn(recentCopyBtn, 1);
            recentGrid.Children.Add(recentCopyBtn);

            row.Child = recentGrid;
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
        var menu = CreateDarkContextMenu();

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
        copyItem.Click += (s, e) => CopyToClipboard(doc.Path);
        menu.Items.Add(copyItem);

        row.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private static ContextMenu CreateDarkContextMenu()
    {
        var menuBg = new WpfSolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E));
        var menuBorder = new WpfSolidColorBrush(WpfColor.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        var itemFg = new WpfSolidColorBrush(WpfColor.FromRgb(0xE0, 0xE0, 0xE0));
        var hoverBg = new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0x4F, 0xC3, 0xF7));
        var transparentBrush = WpfBrushes.Transparent;

        // Build a MenuItem ControlTemplate that fully replaces WPF default chrome
        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "Bd";
        borderFactory.SetValue(Border.BackgroundProperty, transparentBrush);
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        itemTemplate.VisualTree = borderFactory;

        // Hover trigger
        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "Bd"));
        itemTemplate.Triggers.Add(hoverTrigger);

        // MenuItem style using the custom template
        var itemStyle = new Style(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, itemFg));
        itemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, itemTemplate));
        itemStyle.Setters.Add(new Setter(MenuItem.CursorProperty, System.Windows.Input.Cursors.Hand));

        // ContextMenu with custom template to remove system chrome
        var contextMenuTemplate = new ControlTemplate(typeof(ContextMenu));
        var menuBorderFactory = new FrameworkElementFactory(typeof(Border));
        menuBorderFactory.SetValue(Border.BackgroundProperty, menuBg);
        menuBorderFactory.SetValue(Border.BorderBrushProperty, menuBorder);
        menuBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        menuBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        menuBorderFactory.SetValue(Border.PaddingProperty, new Thickness(2, 4, 2, 4));

        var shadowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 12,
            ShadowDepth = 2,
            Opacity = 0.5,
            Color = WpfColor.FromRgb(0, 0, 0)
        };
        menuBorderFactory.SetValue(Border.EffectProperty, shadowEffect);

        var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        menuBorderFactory.AppendChild(itemsPresenter);
        contextMenuTemplate.VisualTree = menuBorderFactory;

        var menu = new ContextMenu
        {
            Template = contextMenuTemplate,
            HasDropShadow = false // We handle shadow ourselves
        };
        menu.Resources[typeof(MenuItem)] = itemStyle;

        return menu;
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

    // ---- Clipboard helpers ----

    private void CopyToClipboard(string text)
        => CopyToClipboard(text, "Path copied");

    private void CopyToClipboard(string text, string statusMessage)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            StatusText.Text = statusMessage;
        }
        catch { }
    }

    private static (string projectNumber, string projectName, string projectNumberAndName) GetProjectHeaderCopyValues(ProjectFileInfo info)
    {
        var display = (info.ProjectName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(display))
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var firstSpace = display.IndexOf(' ');
        if (firstSpace <= 0 || firstSpace >= display.Length - 1)
        {
            // Fallback when scanner only provides a single display token
            return (display, display, display);
        }

        var number = display[..firstSpace].Trim();
        var name = display[(firstSpace + 1)..].Trim();
        return (number, name, $"{number} {name}".Trim());
    }

    private void ShowRecentFileContextMenu(string filePath, Border row)
    {
        var menu = CreateDarkContextMenu();

        var openItem = new MenuItem { Header = "Open" };
        openItem.Click += async (s, e) => await OpenFileAsync(filePath);
        menu.Items.Add(openItem);

        var folderItem = new MenuItem { Header = "Open Folder" };
        folderItem.Click += (s, e) =>
        {
            try { Process.Start("explorer.exe", $"/select,\"{filePath}\""); }
            catch { }
        };
        menu.Items.Add(folderItem);

        var copyItem = new MenuItem { Header = "Copy Path" };
        copyItem.Click += (s, e) => CopyToClipboard(filePath);
        menu.Items.Add(copyItem);

        row.ContextMenu = menu;
        menu.IsOpen = true;
    }

    // ---- Event handlers ----

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _docService.SetSearchQuery(SearchBox.Text);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var parentWindow = System.Windows.Window.GetWindow(this);
        if (parentWindow != null)
        {
            parentWindow.Visibility = Visibility.Hidden;
            parentWindow.Tag = null;
        }
    }

    private async void Discipline_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border btn && btn.Tag is string tag)
        {
            if (e.ClickCount == 2)
            {
                // Double-click: copy discipline folder path
                var info = _docService.ProjectInfo;
                if (info != null)
                {
                    var disciplinePath = Path.Combine(info.ProjectPath, tag);
                    if (Directory.Exists(disciplinePath))
                    {
                        CopyToClipboard(disciplinePath);
                    }
                    else
                    {
                        CopyToClipboard(info.ProjectPath);
                        StatusText.Text = $"{tag} folder not found, copied project path";
                    }
                }
                return;
            }

            if (Enum.TryParse<Discipline>(tag, true, out var disc))
            {
                await _docService.SetDisciplineAsync(disc);
            }
        }
    }

    private void ProjectNameHeader_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            var info = _docService.ProjectInfo;
            if (info != null && !string.IsNullOrEmpty(info.ProjectPath))
            {
                CopyToClipboard(info.ProjectPath);
            }
        }
    }

    private void ProjectNameHeader_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        var info = _docService.ProjectInfo;
        if (info == null)
            return;

        var (projectNumber, projectName, projectNumberAndName) = GetProjectHeaderCopyValues(info);
        var menu = CreateDarkContextMenu();

        var copyNumber = new MenuItem { Header = "Copy Project Number" };
        copyNumber.Click += (s, args) =>
        {
            if (!string.IsNullOrWhiteSpace(projectNumber))
            {
                CopyToClipboard(projectNumber, "Project number copied");
            }
        };
        menu.Items.Add(copyNumber);

        var copyName = new MenuItem { Header = "Copy Project Name" };
        copyName.Click += (s, args) =>
        {
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                CopyToClipboard(projectName, "Project name copied");
            }
        };
        menu.Items.Add(copyName);

        var copyPath = new MenuItem { Header = "Copy Path" };
        copyPath.Click += (s, args) =>
        {
            if (!string.IsNullOrWhiteSpace(info.ProjectPath))
            {
                CopyToClipboard(info.ProjectPath, "Project path copied");
            }
        };
        menu.Items.Add(copyPath);

        var copyNumberAndName = new MenuItem { Header = "Copy Number + Name" };
        copyNumberAndName.Click += (s, args) =>
        {
            if (!string.IsNullOrWhiteSpace(projectNumberAndName))
            {
                CopyToClipboard(projectNumberAndName, "Project number + name copied");
            }
        };
        menu.Items.Add(copyNumberAndName);

        if (sender is FrameworkElement anchor)
        {
            anchor.ContextMenu = menu;
            menu.PlacementTarget = anchor;
        }

        menu.IsOpen = true;
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
