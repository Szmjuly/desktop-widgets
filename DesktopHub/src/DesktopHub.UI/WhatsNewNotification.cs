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
            Background = new SolidColorBrush(WpfColor.FromArgb(0xF5, 0x16, 0x16, 0x16)),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14, 16, 14),
            Width = 460,
            Effect = new DropShadowEffect
            {
                BlurRadius = 26,
                ShadowDepth = 5,
                Opacity = 0.6,
                Color = WpfColor.FromRgb(0, 0, 0)
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
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0xF5, 0xF7, 0xFA))
        });
        headerText.Children.Add(new TextBlock
        {
            Text = $"Updated to v{version}",
            FontSize = 12,
            Foreground = new SolidColorBrush(WpfColor.FromArgb(0xB8, 0xF5, 0xF7, 0xFA)),
            Margin = new Thickness(0, 2, 0, 0)
        });

        Grid.SetColumn(headerText, 2);
        headerGrid.Children.Add(headerText);

        stack.Children.Add(headerGrid);

        stack.Children.Add(new TextBlock
        {
            Text = "Major changes in this release:",
            FontSize = 12,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0xD7, 0xE1, 0xE8)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var notesPanel = new StackPanel
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(0x24, 0xF5, 0xF7, 0xFA)),
            Margin = new Thickness(0, 0, 0, 12)
        };

        foreach (var line in ParseReleaseNotes(releaseNotes))
        {
            notesPanel.Children.Add(new TextBlock
            {
                Text = $"• {line}",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(0xE8, 0xEE, 0xF2)),
                Margin = new Thickness(10, 4, 10, 4)
            });
        }

        stack.Children.Add(notesPanel);

        var actions = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var gotItButton = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(0x33, 0x4F, 0xC3, 0xF7)),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(0x66, 0x4F, 0xC3, 0xF7)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 6, 12, 6),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock
            {
                Text = "Got it",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(0xD7, 0xEE, 0xFF))
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
                "Performance and stability improvements",
                "Bug fixes and quality-of-life polish",
                "Additional refinements across key widgets"
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
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen == null) return;

        var workArea = screen.WorkingArea;
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
