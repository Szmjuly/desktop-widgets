using System.Windows;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class WelcomeWizard : Window
{
    public ScanProfilePresetId? SelectedPreset { get; private set; }
    public bool Skipped { get; private set; }

    public WelcomeWizard()
    {
        InitializeComponent();

        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            WindowHelper.UpdateRootClip(RootBorder, 12, "WelcomeWizard");
            this.Background = null;
        };

        SizeChanged += (s, e) =>
        {
            WindowHelper.UpdateRootClip(RootBorder, 12, "WelcomeWizard");
        };
    }

    private void OnPersonal_Click(object sender, RoutedEventArgs e) => Select(ScanProfilePresetId.Personal);
    private void OnCes_Click(object sender, RoutedEventArgs e) => Select(ScanProfilePresetId.CES);
    private void OnGeneric_Click(object sender, RoutedEventArgs e) => Select(ScanProfilePresetId.GenericNumbered);
    private void OnBlank_Click(object sender, RoutedEventArgs e) => Select(ScanProfilePresetId.Blank);

    private void OnSkip_Click(object sender, RoutedEventArgs e)
    {
        Skipped = true;
        Close();
    }

    private void Select(ScanProfilePresetId preset)
    {
        SelectedPreset = preset;
        Close();
    }

    public static (ScanProfilePresetId? preset, bool skipped) Show(Window? owner = null)
    {
        var dlg = new WelcomeWizard();
        if (owner != null) dlg.Owner = owner;
        dlg.ShowDialog();
        return (dlg.SelectedPreset, dlg.Skipped);
    }
}
