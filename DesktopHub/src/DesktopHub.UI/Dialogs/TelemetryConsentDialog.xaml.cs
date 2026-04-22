using System.Windows;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class TelemetryConsentDialog : Window
{
    public bool Consented { get; private set; }

    private TelemetryConsentDialog()
    {
        InitializeComponent();

        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            WindowHelper.UpdateRootClip(RootBorder, 12, "TelemetryConsentDialog");
            this.Background = null;
        };

        SizeChanged += (s, e) =>
        {
            WindowHelper.UpdateRootClip(RootBorder, 12, "TelemetryConsentDialog");
        };

        Loaded += (s, e) =>
        {
            // DPI-aware centering on the active screen, same pattern as HotkeyConflictDialog.
            WindowHelper.CenterOnCursorScreen(this);
            AcceptButton.Focus();
        };
    }

    private void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        Consented = true;
        Close();
    }

    private void DeclineButton_Click(object sender, RoutedEventArgs e)
    {
        Consented = false;
        Close();
    }

    /// <summary>
    /// Modal show helper. Blocks until the user picks one option.
    /// Returns true if the user consented to telemetry.
    /// Closing the window via X is treated as "decline" (safer default).
    /// </summary>
    public static bool Show(Window? owner = null)
    {
        var dlg = new TelemetryConsentDialog();
        if (owner != null) dlg.Owner = owner;
        dlg.ShowDialog();
        return dlg.Consented;
    }
}
