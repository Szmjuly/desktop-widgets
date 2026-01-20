using System.Windows;
using ProjectSearcher.UI.Helpers;

namespace ProjectSearcher.UI;

public partial class ConfirmationDialog : Window
{
    public bool Result { get; private set; }

    public ConfirmationDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;

        // Setup transparency when window handle is available
        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            UpdateRootClip(12);
            this.Background = null;
        };

        // Update clip on resize
        SizeChanged += (s, e) =>
        {
            UpdateRootClip(12);
        };
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        this.Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        this.Close();
    }

    private void UpdateRootClip(double radiusDip)
    {
        try
        {
            if (RootBorder.ActualWidth <= 0 || RootBorder.ActualHeight <= 0)
            {
                return;
            }
            
            var rect = new System.Windows.Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight);
            RootBorder.Clip = new System.Windows.Media.RectangleGeometry(rect, radiusDip, radiusDip);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"ConfirmationDialog: UpdateRootClip error: {ex.Message}");
        }
    }

    public static bool Show(string message, Window? owner = null)
    {
        var dialog = new ConfirmationDialog(message);
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        dialog.ShowDialog();
        return dialog.Result;
    }
}
