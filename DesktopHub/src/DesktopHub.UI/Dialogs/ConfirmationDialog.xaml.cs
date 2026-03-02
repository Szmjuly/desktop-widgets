using System.Windows;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

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
            WindowHelper.UpdateRootClip(RootBorder, 12, "ConfirmationDialog");
            this.Background = null;
        };

        // Update clip on resize
        SizeChanged += (s, e) =>
        {
            WindowHelper.UpdateRootClip(RootBorder, 12, "ConfirmationDialog");
        };

        // Auto-focus the Exit button when dialog loads
        Loaded += (s, e) =>
        {
            ExitButton.Focus();
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
