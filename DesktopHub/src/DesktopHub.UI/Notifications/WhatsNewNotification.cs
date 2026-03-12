using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfImage = System.Windows.Controls.Image;

namespace DesktopHub.UI;

internal class WhatsNewNotification : Window
{
    private bool _isClosing;

    public WhatsNewNotification(string version, string? releaseNotes)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = WpfBrushes.Transparent;
        Opacity = 0;

        Content = BuildLayout(version, releaseNotes);

        Loaded += (_, _) => PositionBottomRight();
    }

    private UIElement BuildLayout(string version, string? releaseNotes)
    {
        var root = new Border
        {
            Background = Helpers.ThemeHelper.SurfaceSolid,
            BorderBrush = Helpers.ThemeHelper.CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14, 16, 14),
            Width = 460,
            Effect = new DropShadowEffect
            {
                BlurRadius = 26,
                ShadowDepth = 5,
                Opacity = 0.6,
                Color = Helpers.ThemeHelper.GetColor("ShadowColor")
            }
        };

        var stack = new StackPanel();

        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logo = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(7),
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        try
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopHub.UI.DesktopHub_logo.png");
            if (stream != null)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                stream.Dispose();
                logo.Child = new WpfImage { Source = bitmap, Stretch = Stretch.Uniform };
            }
        }
        catch { }

        Grid.SetColumn(logo, 0);
        headerGrid.Children.Add(logo);

        var headerText = new StackPanel();
        headerText.Children.Add(new TextBlock
        {
            Text = "What's New",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Helpers.ThemeHelper.TextPrimary
        });
        headerText.Children.Add(new TextBlock
        {
            Text = $"Updated to v{version}",
            FontSize = 12,
            Foreground = Helpers.ThemeHelper.TextSecondary,
            Margin = new Thickness(0, 2, 0, 0)
        });

        Grid.SetColumn(headerText, 2);
        headerGrid.Children.Add(headerText);

        stack.Children.Add(headerGrid);

        stack.Children.Add(new TextBlock
        {
            Text = "Major changes in this release:",
            FontSize = 12,
            Foreground = Helpers.ThemeHelper.TextSecondary,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var notesPanel = new StackPanel();

        foreach (var line in ParseReleaseNotes(releaseNotes))
        {
            notesPanel.Children.Add(new TextBlock
            {
                Text = $"• {line}",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Helpers.ThemeHelper.TextPrimary,
                Margin = new Thickness(10, 4, 10, 4)
            });
        }

        var notesPanelContainer = new Border
        {
            Background = Helpers.ThemeHelper.FaintOverlay,
            BorderBrush = Helpers.ThemeHelper.CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0, 6, 0, 6),
            Margin = new Thickness(0, 0, 0, 12),
            Child = notesPanel
        };

        stack.Children.Add(notesPanelContainer);

        var actions = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var gotItButton = new Border
        {
            Background = Helpers.ThemeHelper.AccentLight,
            BorderBrush = Helpers.ThemeHelper.Accent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 6, 12, 6),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock
            {
                Text = "Got it",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Helpers.ThemeHelper.TextPrimary
            }
        };
        gotItButton.MouseLeftButtonDown += (_, _) => FadeAndClose();
        actions.Children.Add(gotItButton);

        stack.Children.Add(actions);

        root.Child = stack;
        return root;
    }

    private static IEnumerable<string> ParseReleaseNotes(string? releaseNotes)
    {
        if (string.IsNullOrWhiteSpace(releaseNotes))
        {
            return new[]
            {
                "Dynamic Theming — full DynamicResource brush system across all overlays and widgets",
                "Step-by-Step Guides — Interactive and Visual modes in Cheat Sheets with formula calculators",
                "GEC Wire Reference Table — collapsible circular mil and mm² reference in GEC Calculator",
                "Non-Live Widget Grid — auto-grid positioning with pinned positions per monitor layout",
                "Remote Forced Updates — admin can push updates to specific devices via Firebase",
                "Tag Registry Sync — custom tag keys register to shared Firebase vocabulary automatically",
                "View Mode Toggle Polish — cleaner Table/Lookup switch with consistent label sizing"
            };
        }

        var lines = releaseNotes
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim().TrimStart('-', '*', '•'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .Take(7)
            .ToList();

        if (lines.Count == 0)
        {
            lines.Add("General improvements and bug fixes");
        }

        return lines;
    }

    private void PositionBottomRight()
    {
        var workArea = Helpers.ScreenHelper.GetPrimaryWorkingAreaInDips(this);
        Left = workArea.Right - ActualWidth - 16;
        Top = workArea.Bottom - ActualHeight - 16;
    }

    public new void Show()
    {
        base.Show();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    private void FadeAndClose()
    {
        if (_isClosing) return;
        _isClosing = true;

        var fadeOut = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(180));
        fadeOut.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }
}
