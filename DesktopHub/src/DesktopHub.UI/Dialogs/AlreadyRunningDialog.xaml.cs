using System.Windows;
using System.IO;
using System.IO.Pipes;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class AlreadyRunningDialog : Window
{
    public enum DialogAction
    {
        None,
        OpenOverlay,
        OpenSettings,
        CloseApp
    }

    public DialogAction Action { get; private set; } = DialogAction.None;

    public AlreadyRunningDialog(string hotkeyLabel)
    {
        InitializeComponent();
        HotkeyText.Text = hotkeyLabel;

        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            UpdateRootClip(12);
            this.Background = null;
        };

        SizeChanged += (s, e) =>
        {
            UpdateRootClip(12);
        };
    }

    private void OpenOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        Action = DialogAction.OpenOverlay;
        SendCommandToExistingInstance("SHOW_OVERLAY");
        this.Close();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        Action = DialogAction.OpenSettings;
        SendCommandToExistingInstance("SHOW_SETTINGS");
        this.Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Action = DialogAction.CloseApp;
        SendCommandToExistingInstance("CLOSE_APP");
        this.Close();
    }

    private void SendCommandToExistingInstance(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", "DesktopHub_IPC", PipeDirection.Out);
            client.Connect(1000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(command);
            DebugLogger.Log($"AlreadyRunningDialog: Sent command '{command}' to existing instance");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"AlreadyRunningDialog: Failed to send command: {ex.Message}");
        }
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
            DebugLogger.Log($"AlreadyRunningDialog: UpdateRootClip error: {ex.Message}");
        }
    }

    public static DialogAction Show(string hotkeyLabel, Window? owner = null)
    {
        var dialog = new AlreadyRunningDialog(hotkeyLabel);
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        dialog.ShowDialog();
        return dialog.Action;
    }
}
