using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class InstallCompleteDialog : Window
{
    public InstallCompleteDialog(string installLocation, IReadOnlyList<string>? warnings = null)
    {
        InitializeComponent();
        LocationText.Text = installLocation;

        if (warnings != null && warnings.Count > 0)
        {
            WarningsText.Text = "Some optional features could not be configured:\n"
                + string.Join("\n", warnings.Select(w => "  \u2022  " + w));
            WarningsBorder.Visibility = Visibility.Visible;
        }

        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            WindowHelper.UpdateRootClip(RootBorder, 12, "InstallCompleteDialog");
            this.Background = null;
        };

        SizeChanged += (s, e) =>
        {
            WindowHelper.UpdateRootClip(RootBorder, 12, "InstallCompleteDialog");
        };

        Loaded += (s, e) => GetStartedButton.Focus();
    }

    private void GetStartedButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Modal show helper. Blocks until the user dismisses, then returns.
    /// </summary>
    public static void Show(string installLocation, IReadOnlyList<string>? warnings = null, Window? owner = null)
    {
        var dlg = new InstallCompleteDialog(installLocation, warnings);
        if (owner != null) dlg.Owner = owner;
        dlg.ShowDialog();
    }
}
