using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using DesktopHub.Core.Abstractions;
using DesktopHub.Infrastructure.Settings;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settings;
    private bool _isRecording;
    private int _recordedModifiers;
    private int _recordedKey;
    private Action? _onHotkeyChanged;
    private Action? _onLivingWidgetsModeChanged;
    private bool _isUpdatingSliders;

    public SettingsWindow(ISettingsService settings, Action? onHotkeyChanged = null, Action? onLivingWidgetsModeChanged = null)
    {
        InitializeComponent();
        _settings = settings;
        _onHotkeyChanged = onHotkeyChanged;
        _onLivingWidgetsModeChanged = onLivingWidgetsModeChanged;

        // Setup transparency when window handle is available
        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            
            // DO NOT apply rounded corners to window region - it clips the window
            // WindowBlur.ApplyRoundedCorners(this, 12);
            
            UpdateRootClip(12);
            
            // Ensure window background is completely transparent
            this.Background = null;
        };

        // Update clip on resize
        SizeChanged += (s, e) =>
        {
            // DO NOT apply window region rounding - causes clipping
            // WindowBlur.ApplyRoundedCorners(this, 12);
            UpdateRootClip(12);
        };

        // Load settings after initialization
        this.Loaded += async (s, e) =>
        {
            // DISABLE blur - it renders as solid black
            // WindowBlur.EnableBlur(this, useAcrylic: true);
            
            await LoadSettingsAsync();
        };
    }

    private void UpdateHotkeyWarning(int modifiers, int key)
    {
        if (HotkeyTypingWarning == null || HotkeyTypingWarningText == null)
        {
            return;
        }

        if (IsShiftCharacterHotkey(modifiers, key))
        {
            HotkeyTypingWarningText.Text =
                "Heads up: Shift+character shortcuts (like Shift+A or Shift+1) are treated as normal typing. " +
                "When a text field is focused, the hotkey is suppressed to avoid blocking input. " +
                "Use Esc or click outside the overlay to close it while typing.";
            HotkeyTypingWarning.Visibility = Visibility.Visible;
        }
        else
        {
            HotkeyTypingWarning.Visibility = Visibility.Collapsed;
        }
    }

    private static bool IsShiftCharacterHotkey(int modifiers, int key)
    {
        if ((modifiers & (int)GlobalHotkey.MOD_SHIFT) == 0)
        {
            return false;
        }

        var wpfKey = KeyInterop.KeyFromVirtualKey(key);
        return IsCharacterKey(wpfKey);
    }

    private static bool IsCharacterKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            return true;
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return true;
        }

        return key is Key.Oem1
            or Key.Oem2
            or Key.Oem3
            or Key.Oem4
            or Key.Oem5
            or Key.Oem6
            or Key.Oem7
            or Key.Oem8
            or Key.OemPlus
            or Key.OemMinus
            or Key.OemComma
            or Key.OemPeriod
            or Key.OemQuestion
            or Key.OemTilde
            or Key.OemOpenBrackets
            or Key.OemCloseBrackets
            or Key.OemPipe
            or Key.OemSemicolon
            or Key.OemQuotes;
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            // Ensure settings are loaded
            await _settings.LoadAsync();
            
            var (modifiers, key) = _settings.GetHotkey();
            HotkeyText.Text = FormatHotkey(modifiers, key);
            UpdateHotkeyWarning(modifiers, key);

            AutoStartToggle.IsChecked = _settings.GetAutoStart();
            LivingWidgetsModeToggle.IsChecked = _settings.GetLivingWidgetsMode();
            
            var qDrivePath = _settings.GetQDrivePath();
            QDrivePathBox.Text = string.IsNullOrEmpty(qDrivePath) ? "Q:\\" : qDrivePath;
            
            // Load transparency settings
            _isUpdatingSliders = true;
            SettingsTransparencySlider.Value = _settings.GetSettingsTransparency();
            OverlayTransparencySlider.Value = _settings.GetOverlayTransparency();
            _isUpdatingSliders = false;
            
            // Load notification duration setting
            LoadNotificationDurationSetting();
            
            UpdateLinkButtonIcon();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SettingsWindow: LoadSettingsAsync error: {ex.Message}");
            if (StatusText != null)
            {
                StatusText.Text = $"Failed to load settings: {ex.Message}";
            }
        }
    }

    private void HotkeyBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        StartRecording();
    }

    private void StartRecording()
    {
        _isRecording = true;
        HotkeyText.Visibility = Visibility.Collapsed;
        RecordingText.Visibility = Visibility.Visible;
        HotkeyBox.BorderBrush = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
        this.Focus();
    }

    private void StopRecording()
    {
        _isRecording = false;
        RecordingText.Visibility = Visibility.Collapsed;
        HotkeyText.Visibility = Visibility.Visible;
        HotkeyBox.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_isRecording)
            {
                StopRecording();
                e.Handled = true;
                return;
            }
            this.Close();
            e.Handled = true;
            return;
        }

        if (!_isRecording)
            return;

        // Ignore modifier-only keys
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LWin || e.Key == Key.RWin)
        {
            e.Handled = true;
            return;
        }

        // Record the hotkey
        _recordedModifiers = 0;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            _recordedModifiers |= (int)GlobalHotkey.MOD_CONTROL;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            _recordedModifiers |= (int)GlobalHotkey.MOD_ALT;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            _recordedModifiers |= (int)GlobalHotkey.MOD_SHIFT;
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
            _recordedModifiers |= (int)GlobalHotkey.MOD_WIN;

        _recordedKey = KeyInterop.VirtualKeyFromKey(e.Key);

        // Update display
        var hotkeyLabel = FormatHotkey(_recordedModifiers, _recordedKey);
        HotkeyText.Text = hotkeyLabel;
        UpdateHotkeyWarning(_recordedModifiers, _recordedKey);

        // Save the new hotkey
        _settings.SetHotkey(_recordedModifiers, _recordedKey);
        _ = _settings.SaveAsync();

        StopRecording();
        StatusText.Text = "Hotkey updated!";

        // Notify parent to update hotkey
        _onHotkeyChanged?.Invoke();

        e.Handled = true;
    }

    private void ResetHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        const int defaultModifiers = 0x0003; // Ctrl+Alt
        const int defaultKey = 0x20; // Space

        _settings.SetHotkey(defaultModifiers, defaultKey);
        _ = _settings.SaveAsync();

        HotkeyText.Text = FormatHotkey(defaultModifiers, defaultKey);
        UpdateHotkeyWarning(defaultModifiers, defaultKey);
        StatusText.Text = "Hotkey reset to default.";

        _onHotkeyChanged?.Invoke();
    }

    private async void AutoStartToggle_Checked(object sender, RoutedEventArgs e)
    {
        _settings.SetAutoStart(true);
        await _settings.SaveAsync();
        SetAutoStart(true);
        StatusText.Text = "Auto-start enabled";
    }

    private async void AutoStartToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _settings.SetAutoStart(false);
        await _settings.SaveAsync();
        SetAutoStart(false);
        StatusText.Text = "Auto-start disabled";
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null)
                return;

            const string appName = "DesktopHub";

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(appName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to update auto-start: {ex.Message}";
        }
    }

    private async void QDrivePathBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        try
        {
            if (_settings == null)
            {
                DebugLogger.Log("SettingsWindow: _settings is null in QDrivePathBox_TextChanged");
                return;
            }
            
            var path = QDrivePathBox.Text;
            if (!string.IsNullOrWhiteSpace(path))
            {
                _settings.SetQDrivePath(path);
                await _settings.SaveAsync();
                if (StatusText != null)
                {
                    StatusText.Text = "Q: drive path updated";
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SettingsWindow: QDrivePathBox_TextChanged error: {ex.Message}");
            if (StatusText != null)
            {
                StatusText.Text = $"Error updating path: {ex.Message}";
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private static string FormatHotkey(int modifiers, int key)
    {
        var parts = new List<string>();

        if ((modifiers & (int)GlobalHotkey.MOD_CONTROL) != 0)
            parts.Add("Ctrl");

        if ((modifiers & (int)GlobalHotkey.MOD_ALT) != 0)
            parts.Add("Alt");

        if ((modifiers & (int)GlobalHotkey.MOD_SHIFT) != 0)
            parts.Add("Shift");

        if ((modifiers & (int)GlobalHotkey.MOD_WIN) != 0)
            parts.Add("Win");

        var keyLabel = KeyInterop.KeyFromVirtualKey(key);
        var keyText = keyLabel != Key.None ? keyLabel.ToString() : $"0x{key:X}";
        parts.Add(keyText);

        return string.Join("+", parts);
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
            DebugLogger.Log($"SettingsWindow: UpdateRootClip error: {ex.Message}");
        }
    }

    private void SettingsScroll_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
    }

    private void SettingsTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RootBorder == null || _isUpdatingSliders || _settings == null) return;
        
        var alpha = (byte)(e.NewValue * 255);
        var color = System.Windows.Media.Color.FromArgb(alpha, 0x18, 0x18, 0x18);
        RootBorder.Background = new System.Windows.Media.SolidColorBrush(color);
        
        _settings.SetSettingsTransparency(e.NewValue);
        _ = _settings.SaveAsync();
        
        // If linked, update overlay slider too
        if (_settings.GetTransparencyLinked())
        {
            _isUpdatingSliders = true;
            if (OverlayTransparencySlider != null)
            {
                OverlayTransparencySlider.Value = e.NewValue;
            }
            _settings.SetOverlayTransparency(e.NewValue);
            _isUpdatingSliders = false;
        }
    }

    private void OverlayTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSliders || _settings == null) return;
        
        _settings.SetOverlayTransparency(e.NewValue);
        _ = _settings.SaveAsync();
        
        // If linked, update settings slider too
        if (_settings.GetTransparencyLinked())
        {
            _isUpdatingSliders = true;
            if (SettingsTransparencySlider != null)
            {
                SettingsTransparencySlider.Value = e.NewValue;
            }
            _settings.SetSettingsTransparency(e.NewValue);
            _isUpdatingSliders = false;
            
            if (RootBorder != null)
            {
                var alpha = (byte)(e.NewValue * 255);
                var color = System.Windows.Media.Color.FromArgb(alpha, 0x18, 0x18, 0x18);
                RootBorder.Background = new System.Windows.Media.SolidColorBrush(color);
            }
        }
    }

    private void LinkTransparencyButton_Click(object sender, RoutedEventArgs e)
    {
        var isLinked = _settings.GetTransparencyLinked();
        _settings.SetTransparencyLinked(!isLinked);
        _ = _settings.SaveAsync();
        
        UpdateLinkButtonIcon();
        
        // If linking, sync the values
        if (!isLinked)
        {
            _isUpdatingSliders = true;
            var settingsValue = SettingsTransparencySlider.Value;
            OverlayTransparencySlider.Value = settingsValue;
            _settings.SetOverlayTransparency(settingsValue);
            _isUpdatingSliders = false;
        }
        
        StatusText.Text = !isLinked ? "Transparency linked" : "Transparency unlinked";
    }

    private void UpdateLinkButtonIcon()
    {
        var isLinked = _settings.GetTransparencyLinked();
        // Find the TextBlock in the button template
        if (LinkTransparencyButton.Template?.FindName("LinkIcon", LinkTransparencyButton) is System.Windows.Controls.TextBlock linkIcon)
        {
            linkIcon.Text = isLinked ? "🔗" : "🔓";
        }

        // Grey out sliders when linked but keep them interactive
        var linkedOpacity = isLinked ? 0.6 : 1.0;
        SettingsTransparencySlider.Opacity = linkedOpacity;
        OverlayTransparencySlider.Opacity = linkedOpacity;
    }

    private void LoadNotificationDurationSetting()
    {
        var durationMs = _settings.GetNotificationDurationMs();
        
        // Select the appropriate radio button based on duration
        if (durationMs <= 0)
        {
            NotificationDisabledRadio.IsChecked = true;
        }
        else if (durationMs <= 2500)
        {
            NotificationShortRadio.IsChecked = true;
        }
        else if (durationMs <= 3500)
        {
            NotificationMediumRadio.IsChecked = true;
        }
        else
        {
            NotificationLongRadio.IsChecked = true;
        }
    }

    private void NotificationDurationRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton radioButton && radioButton.IsChecked == true)
        {
            int newDurationMs;
            
            if (radioButton == NotificationShortRadio)
            {
                newDurationMs = 2000; // 2 seconds
            }
            else if (radioButton == NotificationMediumRadio)
            {
                newDurationMs = 3000; // 3 seconds
            }
            else if (radioButton == NotificationLongRadio)
            {
                newDurationMs = 5000; // 5 seconds
            }
            else if (radioButton == NotificationDisabledRadio)
            {
                newDurationMs = 0; // Disabled
            }
            else
            {
                return; // Unknown radio button
            }

            _settings.SetNotificationDurationMs(newDurationMs);
            _ = _settings.SaveAsync();
            
            // Update status text
            var statusText = newDurationMs switch
            {
                0 => "Notifications disabled",
                2000 => "Notification duration: Short (2 seconds)",
                3000 => "Notification duration: Medium (3 seconds)",
                5000 => "Notification duration: Long (5 seconds)",
                _ => "Notification duration updated"
            };
            
            StatusText.Text = statusText;
        }
    }

    private async void LivingWidgetsModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        _settings.SetLivingWidgetsMode(true);
        await _settings.SaveAsync();
        StatusText.Text = "Living Widgets Mode enabled - widgets are now draggable and pinnable";
        _onLivingWidgetsModeChanged?.Invoke();
    }

    private async void LivingWidgetsModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _settings.SetLivingWidgetsMode(false);
        await _settings.SaveAsync();
        StatusText.Text = "Living Widgets Mode disabled - widgets will auto-hide when clicking away";
        _onLivingWidgetsModeChanged?.Invoke();
    }
}
