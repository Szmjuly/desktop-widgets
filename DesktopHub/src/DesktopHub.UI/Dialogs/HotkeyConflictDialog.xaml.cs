using System.Diagnostics;
using System.Windows;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class HotkeyConflictDialog : Window
{
    public enum DialogAction
    {
        None,
        OpenSettings,
        Dismiss,
        Ok,
        OpenedConflictingApp
    }

    public DialogAction Action { get; private set; } = DialogAction.None;

    private Process? _conflictingProcess;

    private HotkeyConflictDialog()
    {
        InitializeComponent();

        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            WindowHelper.UpdateRootClip(RootBorder, 12, "HotkeyConflictDialog");
            this.Background = null;
        };

        SizeChanged += (s, e) =>
        {
            WindowHelper.UpdateRootClip(RootBorder, 12, "HotkeyConflictDialog");
        };

        Loaded += (s, e) => PrimaryButton.Focus();
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        Action = PrimaryButton.Content?.ToString() == "Open Settings"
            ? DialogAction.OpenSettings
            : DialogAction.Ok;
        this.Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        Action = DialogAction.Dismiss;
        this.Close();
    }

    private void OpenConflictingAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (_conflictingProcess != null)
        {
            KnownHotkeyRegistry.BringToForeground(_conflictingProcess);
        }
        Action = DialogAction.OpenedConflictingApp;
        this.Close();
    }

    /// <summary>
    /// If this combo is owned by a known running app, reveal the "Open [App]" button and
    /// update the hint so the user knows which app we've identified.
    /// </summary>
    private void TryAttachConflictingApp(int modifiers, int key)
    {
        var match = KnownHotkeyRegistry.FindRunningConflict(modifiers, key);
        if (match == null) return;

        _conflictingProcess = match.Process;
        OpenConflictingAppButton.Content = $"Open {match.DisplayName}";
        OpenConflictingAppButton.Visibility = Visibility.Visible;
        HintText.Text =
            $"It looks like {match.DisplayName} is currently using this shortcut. " +
            $"You can open {match.DisplayName} to change or close it, or pick a different combo here.";
    }

    /// <summary>
    /// Shown when the user tries to set a hotkey in Settings that another app already owns.
    /// </summary>
    public static DialogAction ShowForSettingsConflict(string hotkeyLabel, int modifiers, int key, Window? owner = null)
    {
        var dialog = new HotkeyConflictDialog
        {
            Owner = owner,
        };
        dialog.TitleText.Text = "Shortcut Unavailable";
        dialog.SubtitleText.Text = "Another application has already registered this shortcut on Windows.";
        dialog.HotkeyText.Text = hotkeyLabel;
        dialog.HintText.Text =
            "Windows only allows one app to own a shortcut at a time. Common culprits: Claude Desktop, PowerToys, " +
            "Copilot, or other launchers. Try a different combo, or close the conflicting app and try again.";
        dialog.PrimaryButton.Content = "OK";
        dialog.SecondaryButton.Visibility = Visibility.Collapsed;
        dialog.TryAttachConflictingApp(modifiers, key);
        dialog.ShowDialog();
        return dialog.Action;
    }

    /// <summary>
    /// Shown when the user tries to set a hotkey in Settings that another group in this app already uses.
    /// </summary>
    public static DialogAction ShowForInternalConflict(string hotkeyLabel, int otherGroupIndex, Window? owner = null)
    {
        var dialog = new HotkeyConflictDialog
        {
            Owner = owner,
        };
        dialog.TitleText.Text = "Shortcut Already Assigned";
        dialog.SubtitleText.Text = $"This shortcut is already assigned to Group {otherGroupIndex} in DesktopHub.";
        dialog.HotkeyText.Text = hotkeyLabel;
        dialog.HintText.Text =
            "Each hotkey group needs a unique combination. Remove the shortcut from the other group first, " +
            "or pick a different combo for this one.";
        dialog.PrimaryButton.Content = "OK";
        dialog.SecondaryButton.Visibility = Visibility.Collapsed;
        // Internal conflict → no external app to identify
        dialog.ShowDialog();
        return dialog.Action;
    }

    /// <summary>
    /// Shown at app startup when one or more saved hotkeys cannot be registered (external conflict).
    /// </summary>
    public static DialogAction ShowForStartup(string hotkeyLabel, int modifiers, int key, Window? owner = null)
    {
        var dialog = new HotkeyConflictDialog();
        if (owner != null && owner.IsLoaded)
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        dialog.TitleText.Text = "Shortcut Unavailable";
        dialog.SubtitleText.Text = "DesktopHub couldn't register its global shortcut because another app is already using it.";
        dialog.HotkeyText.Text = hotkeyLabel;
        dialog.HintText.Text =
            "DesktopHub is running \u2014 you can still open it from the system tray. To use a global shortcut, " +
            "open Settings and pick a combo that isn't taken by another app.";
        dialog.PrimaryButton.Content = "Open Settings";
        dialog.SecondaryButton.Content = "Dismiss";
        dialog.SecondaryButton.Visibility = Visibility.Visible;
        dialog.TryAttachConflictingApp(modifiers, key);
        dialog.ShowDialog();
        return dialog.Action;
    }
}
