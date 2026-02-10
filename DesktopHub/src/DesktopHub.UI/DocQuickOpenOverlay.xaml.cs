using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Services;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class DocQuickOpenOverlay : Window
{
    private readonly DocOpenService _docService;
    private readonly ISettingsService _settings;
    private readonly DocQuickOpenWidget _widget;
    private bool _isDragging = false;
    private System.Windows.Point _dragStartPoint;
    private bool _isLivingWidgetsMode = false;

    public DocQuickOpenWidget Widget => _widget;

    public DocQuickOpenOverlay(DocOpenService docService, ISettingsService settings)
    {
        InitializeComponent();
        _docService = docService;
        _settings = settings;

        _widget = new DocQuickOpenWidget(docService);
        WidgetHost.Content = _widget;
    }

    public void UpdateTransparency()
    {
        try
        {
            var transparency = _settings.GetDocWidgetTransparency();
            var alpha = (byte)(transparency * 255);

            if (RootBorder != null)
            {
                RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x12, 0x12, 0x12));
            }
        }
        catch { }
    }

    public void EnableDragging()
    {
        _isLivingWidgetsMode = true;
        
        this.MouseLeftButtonDown -= Overlay_MouseLeftButtonDown;
        this.MouseLeftButtonUp -= Overlay_MouseLeftButtonUp;
        this.MouseMove -= Overlay_MouseMove;
        
        this.MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
        this.MouseLeftButtonUp += Overlay_MouseLeftButtonUp;
        this.MouseMove += Overlay_MouseMove;
    }
    
    public void DisableDragging()
    {
        _isLivingWidgetsMode = false;
        this.MouseLeftButtonDown -= Overlay_MouseLeftButtonDown;
        this.MouseLeftButtonUp -= Overlay_MouseLeftButtonUp;
        this.MouseMove -= Overlay_MouseMove;
    }
    
    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isLivingWidgetsMode) return;
        
        var element = e.OriginalSource as FrameworkElement;
        if (element != null)
        {
            var clickedType = element.GetType().Name;
            if (clickedType == "TextBox" || clickedType == "Button" || clickedType == "ListBoxItem" ||
                clickedType == "ComboBox" || clickedType == "ScrollBar" || clickedType == "Thumb")
                return;
        }
        
        _isDragging = true;
        _dragStartPoint = e.GetPosition(this);
        this.CaptureMouse();
    }
    
    private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();
        }
    }
    
    private void Overlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var currentPosition = e.GetPosition(this);
            var offset = currentPosition - _dragStartPoint;
            this.Left += offset.X;
            this.Top += offset.Y;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            this.Visibility = Visibility.Hidden;
            this.Tag = null;
            e.Handled = true;
        }
    }
}
