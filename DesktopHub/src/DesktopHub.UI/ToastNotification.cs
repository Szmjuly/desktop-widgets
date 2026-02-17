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

internal class ToastNotification : Window
{
    private readonly int _durationMs;
    private System.Windows.Threading.DispatcherTimer? _autoCloseTimer;
    private System.Windows.Point _dragStartScreen; // device pixels, stable across window moves
    private double _dragOriginLeft;
    private bool _isDragging;
    private bool _dismissing;

    public ToastNotification(string title, string message, int durationMs)
    {
        _durationMs = durationMs;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = WpfBrushes.Transparent;
        Opacity = 0;

        Content = BuildLayout(title, message);

        Loaded += (s, e) => PositionBottomRight();

        MouseLeftButtonDown += OnMouseDown;
        MouseLeftButtonUp += OnMouseUp;
        MouseMove += OnMouseMove;
        Cursor = System.Windows.Input.Cursors.Hand;
    }

    private UIElement BuildLayout(string title, string message)
    {
        var root = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(0xF2, 0x18, 0x18, 0x18)),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(0x35, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Width = 300,
            Effect = new DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 4,
                Opacity = 0.6,
                Color = WpfColor.FromRgb(0, 0, 0)
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // App logo
        var logo = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        try
        {
            var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("DesktopHub.UI.DesktopHub_logo.png");
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
        grid.Children.Add(logo);

        // Title + message
        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        textStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0xF5, 0xF7, 0xFA)),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        textStack.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 11,
            Foreground = new SolidColorBrush(WpfColor.FromArgb(0x99, 0xF5, 0xF7, 0xFA)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        });

        Grid.SetColumn(textStack, 2);
        grid.Children.Add(textStack);

        root.Child = grid;
        return root;
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

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        BeginAnimation(OpacityProperty, fadeIn);

        _autoCloseTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_durationMs)
        };
        _autoCloseTimer.Tick += (s, e) =>
        {
            _autoCloseTimer.Stop();
            FadeAndClose();
        };
        _autoCloseTimer.Start();
    }

    private double GetDpiScale()
        => PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Use screen (device-pixel) coords so the reference point doesn't shift as the window moves
        _dragStartScreen = PointToScreen(e.GetPosition(this));
        _dragOriginLeft = Left;
        _isDragging = true;
        CaptureMouse();
    }

    private const double FadeDistance = 180.0; // DIPs of drag needed to reach fully transparent
    private const double SwipeThreshold = 60.0;

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        var screenNow = PointToScreen(e.GetPosition(this));
        var dpi = GetDpiScale();
        var deltaX = (screenNow.X - _dragStartScreen.X) / dpi;
        Left = _dragOriginLeft + deltaX;
        // Fade proportionally to how far the user has dragged
        Opacity = Math.Max(0.0, 1.0 - Math.Abs(deltaX) / FadeDistance);

        // Dismiss as soon as swipe threshold is crossed during drag (no need to release)
        if (Math.Abs(deltaX) >= SwipeThreshold)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            _autoCloseTimer?.Stop();
            SlideAndClose(deltaX > 0);
        }
    }

    private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();

        var screenNow = PointToScreen(e.GetPosition(this));
        var dpi = GetDpiScale();
        var deltaX = (screenNow.X - _dragStartScreen.X) / dpi;
        var deltaY = (screenNow.Y - _dragStartScreen.Y) / dpi;

        if (Math.Abs(deltaX) > SwipeThreshold)
        {
            // Swipe — slide the rest of the way off screen
            _autoCloseTimer?.Stop();
            SlideAndClose(deltaX > 0);
        }
        else if (Math.Abs(deltaX) < 8 && Math.Abs(deltaY) < 8)
        {
            // Click — instant dismiss
            _autoCloseTimer?.Stop();
            FadeAndClose();
        }
        else
        {
            // Small drag that didn't reach threshold — snap position and opacity back
            var easing = new System.Windows.Media.Animation.CubicEase
                { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            BeginAnimation(LeftProperty, new DoubleAnimation(_dragOriginLeft, TimeSpan.FromMilliseconds(150))
                { EasingFunction = easing });
            BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150))
                { EasingFunction = easing });
        }
    }

    private void FadeAndClose()
    {
        if (_dismissing) return;
        _dismissing = true;
        var fadeOut = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void SlideAndClose(bool toRight)
    {
        if (_dismissing) return;
        _dismissing = true;

        // Move a local distance from the current position so it disappears quickly,
        // instead of sliding across entire virtual desktop widths on multi-monitor setups.
        var targetLeft = toRight
            ? Left + ActualWidth + 40
            : Left - ActualWidth - 40;

        // Opacity is already partially reduced from the live drag — animate from current value
        var remainingMs = (int)(Opacity * 160); // faster finish the more transparent it already is
        var easing = new System.Windows.Media.Animation.CubicEase
            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };
        BeginAnimation(LeftProperty, new DoubleAnimation(Left, targetLeft, TimeSpan.FromMilliseconds(remainingMs))
            { EasingFunction = easing });
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(remainingMs));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }
}
