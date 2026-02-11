using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using DesktopHub.Core.Abstractions;
using DesktopHub.Infrastructure.Settings;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;

namespace DesktopHub.UI;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settings;
    private readonly TaskService? _taskService;
    private readonly DocOpenService? _docService;
    private bool _isRecording;
    private int _recordedModifiers;
    private int _recordedKey;
    private bool _isRecordingCloseShortcut;
    private int _recordedCloseShortcutModifiers;
    private int _recordedCloseShortcutKey;
    private Action? _onHotkeyChanged;
    private Action? _onCloseShortcutChanged;
    private Action? _onLivingWidgetsModeChanged;
    private Action? _onDriveSettingsChanged;
    private Action? _onTransparencyChanged;
    private Action? _onSearchWidgetEnabledChanged;
    private Action? _onTimerWidgetEnabledChanged;
    private Action? _onQuickTasksWidgetEnabledChanged;
    private Action? _onDocWidgetEnabledChanged;
    private Action? _onUpdateSettingsChanged;
    private bool _isUpdatingSliders;
    private bool _isLoadingQTSettings;
    private bool _isLoadingDQSettings;

    public SettingsWindow(ISettingsService settings, Action? onHotkeyChanged = null, Action? onCloseShortcutChanged = null, Action? onLivingWidgetsModeChanged = null, Action? onDriveSettingsChanged = null, Action? onTransparencyChanged = null, TaskService? taskService = null, DocOpenService? docService = null, Action? onSearchWidgetEnabledChanged = null, Action? onTimerWidgetEnabledChanged = null, Action? onQuickTasksWidgetEnabledChanged = null, Action? onDocWidgetEnabledChanged = null, Action? onUpdateSettingsChanged = null)
    {
        _settings = settings;
        _taskService = taskService;
        _docService = docService;
        _onHotkeyChanged = onHotkeyChanged;
        _onCloseShortcutChanged = onCloseShortcutChanged;
        _onLivingWidgetsModeChanged = onLivingWidgetsModeChanged;
        _onDriveSettingsChanged = onDriveSettingsChanged;
        _onTransparencyChanged = onTransparencyChanged;
        _onSearchWidgetEnabledChanged = onSearchWidgetEnabledChanged;
        _onTimerWidgetEnabledChanged = onTimerWidgetEnabledChanged;
        _onQuickTasksWidgetEnabledChanged = onQuickTasksWidgetEnabledChanged;
        _onDocWidgetEnabledChanged = onDocWidgetEnabledChanged;
        _onUpdateSettingsChanged = onUpdateSettingsChanged;

        // Suppress all slider/control events during XAML initialization
        _isUpdatingSliders = true;
        _isLoadingQTSettings = true;
        _isLoadingDQSettings = true;

        InitializeComponent();

        // Re-enable event handlers now that all XAML elements exist
        _isUpdatingSliders = false;
        _isLoadingQTSettings = false;
        _isLoadingDQSettings = false;

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

            var (closeModifiers, closeKey) = _settings.GetCloseShortcut();
            CloseShortcutText.Text = FormatHotkey(closeModifiers, closeKey);

            AutoStartToggle.IsChecked = _settings.GetAutoStart();
            LivingWidgetsModeToggle.IsChecked = _settings.GetLivingWidgetsMode();
            
            // Load drive settings
            QDriveEnabledToggle.IsChecked = _settings.GetQDriveEnabled();
            var qDrivePath = _settings.GetQDrivePath();
            QDrivePathBox.Text = string.IsNullOrEmpty(qDrivePath) ? "Q:\\" : qDrivePath;
            
            PDriveEnabledToggle.IsChecked = _settings.GetPDriveEnabled();
            var pDrivePath = _settings.GetPDrivePath();
            PDrivePathBox.Text = string.IsNullOrEmpty(pDrivePath) ? "P:\\" : pDrivePath;
            
            // Load transparency settings
            _isUpdatingSliders = true;
            SettingsTransparencySlider.Value = _settings.GetSettingsTransparency();
            OverlayTransparencySlider.Value = _settings.GetOverlayTransparency();
            WidgetLauncherTransparencySlider.Value = _settings.GetWidgetLauncherTransparency();
            TimerWidgetTransparencySlider.Value = _settings.GetTimerWidgetTransparency();
            QuickTasksTransparencySlider.Value = _settings.GetQuickTasksWidgetTransparency();
            DocTransparencySlider.Value = _settings.GetDocWidgetTransparency();
            _isUpdatingSliders = false;
            
            // Load widget enabled toggles
            TimerWidgetEnabledToggle.IsChecked = _settings.GetTimerWidgetEnabled();
            QuickTasksWidgetEnabledToggle.IsChecked = _settings.GetQuickTasksWidgetEnabled();
            DocWidgetEnabledToggle.IsChecked = _settings.GetDocWidgetEnabled();
            
            // Load search widget enabled toggle
            SearchWidgetEnabledToggle.IsChecked = _settings.GetSearchWidgetEnabled();
            
            // Load update settings
            AutoUpdateCheckToggle.IsChecked = _settings.GetAutoUpdateCheckEnabled();
            AutoUpdateInstallToggle.IsChecked = _settings.GetAutoUpdateInstallEnabled();
            LoadUpdateFrequencyCombo();
            
            // Load notification duration setting
            LoadNotificationDurationSetting();
            
            // Load Quick Tasks config
            LoadQuickTasksSettings();
            
            UpdateAllLinkButtons();
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
            if (_isRecordingCloseShortcut)
            {
                StopRecordingCloseShortcut();
                e.Handled = true;
                return;
            }
            this.Close();
            e.Handled = true;
            return;
        }

        if (_isRecording)
        {
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
        else if (_isRecordingCloseShortcut)
        {
            // Ignore modifier-only keys
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            // Record the close shortcut
            _recordedCloseShortcutModifiers = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                _recordedCloseShortcutModifiers |= (int)GlobalHotkey.MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                _recordedCloseShortcutModifiers |= (int)GlobalHotkey.MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                _recordedCloseShortcutModifiers |= (int)GlobalHotkey.MOD_SHIFT;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
                _recordedCloseShortcutModifiers |= (int)GlobalHotkey.MOD_WIN;

            _recordedCloseShortcutKey = KeyInterop.VirtualKeyFromKey(e.Key);

            // Update display
            var closeShortcutLabel = FormatHotkey(_recordedCloseShortcutModifiers, _recordedCloseShortcutKey);
            CloseShortcutText.Text = closeShortcutLabel;

            // Save the new close shortcut
            _settings.SetCloseShortcut(_recordedCloseShortcutModifiers, _recordedCloseShortcutKey);
            _ = _settings.SaveAsync();

            StopRecordingCloseShortcut();
            StatusText.Text = "Close shortcut updated!";

            // Notify parent to update close shortcut
            _onCloseShortcutChanged?.Invoke();

            e.Handled = true;
        }
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

    private void CloseShortcutBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        StartRecordingCloseShortcut();
    }

    private void StartRecordingCloseShortcut()
    {
        _isRecordingCloseShortcut = true;
        CloseShortcutText.Visibility = Visibility.Collapsed;
        CloseShortcutRecordingText.Visibility = Visibility.Visible;
        CloseShortcutBox.BorderBrush = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
        this.Focus();
    }

    private void StopRecordingCloseShortcut()
    {
        _isRecordingCloseShortcut = false;
        CloseShortcutRecordingText.Visibility = Visibility.Collapsed;
        CloseShortcutText.Visibility = Visibility.Visible;
        CloseShortcutBox.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
    }

    private void ResetCloseShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        const int defaultModifiers = 0x0000; // No modifiers
        const int defaultKey = 0x1B; // ESC

        _settings.SetCloseShortcut(defaultModifiers, defaultKey);
        _ = _settings.SaveAsync();

        CloseShortcutText.Text = FormatHotkey(defaultModifiers, defaultKey);
        StatusText.Text = "Close shortcut reset to default.";

        _onCloseShortcutChanged?.Invoke();
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

    private async void PDrivePathBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        try
        {
            if (_settings == null)
            {
                DebugLogger.Log("SettingsWindow: _settings is null in PDrivePathBox_TextChanged");
                return;
            }
            
            var path = PDrivePathBox.Text;
            if (!string.IsNullOrWhiteSpace(path))
            {
                _settings.SetPDrivePath(path);
                await _settings.SaveAsync();
                if (StatusText != null)
                {
                    StatusText.Text = "P: drive path updated";
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SettingsWindow: PDrivePathBox_TextChanged error: {ex.Message}");
            if (StatusText != null)
            {
                StatusText.Text = $"Error updating path: {ex.Message}";
            }
        }
    }

    private async void QDriveEnabledToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetQDriveEnabled(true);
        await _settings.SaveAsync();
        if (StatusText != null)
        {
            StatusText.Text = "Q: drive enabled - updating projects...";
        }
        _onDriveSettingsChanged?.Invoke();
    }

    private async void QDriveEnabledToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetQDriveEnabled(false);
        await _settings.SaveAsync();
        if (StatusText != null)
        {
            StatusText.Text = "Q: drive disabled - updating projects...";
        }
        _onDriveSettingsChanged?.Invoke();
    }

    private async void PDriveEnabledToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetPDriveEnabled(true);
        await _settings.SaveAsync();
        if (StatusText != null)
        {
            StatusText.Text = "P: drive enabled - scanning...";
        }
        _onDriveSettingsChanged?.Invoke();
        
        // Wait for scan to complete and update status
        await Task.Delay(3000);
        if (StatusText != null && StatusText.Text.Contains("scanning"))
        {
            StatusText.Text = "P: drive enabled - scan complete";
        }
    }

    private async void PDriveEnabledToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetPDriveEnabled(false);
        await _settings.SaveAsync();
        if (StatusText != null)
        {
            StatusText.Text = "P: drive disabled - updating projects...";
        }
        _onDriveSettingsChanged?.Invoke();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            // Ensure window is active before dragging to avoid "stuck" behavior
            this.Activate();
            this.DragMove();
        }
    }

    private void MenuButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton radioButton)
        {
            // Hide all panels
            if (ShortcutsPanel != null) ShortcutsPanel.Visibility = Visibility.Collapsed;
            if (AppearancePanel != null) AppearancePanel.Visibility = Visibility.Collapsed;
            if (GeneralPanel != null) GeneralPanel.Visibility = Visibility.Collapsed;
            if (QuickTasksPanel != null) QuickTasksPanel.Visibility = Visibility.Collapsed;
            if (DocQuickOpenPanel != null) DocQuickOpenPanel.Visibility = Visibility.Collapsed;
            if (UpdatesPanel != null) UpdatesPanel.Visibility = Visibility.Collapsed;

            // Show selected panel
            if (radioButton.Name == "ShortcutsMenuButton" && ShortcutsPanel != null)
            {
                ShortcutsPanel.Visibility = Visibility.Visible;
            }
            else if (radioButton.Name == "AppearanceMenuButton" && AppearancePanel != null)
            {
                AppearancePanel.Visibility = Visibility.Visible;
            }
            else if (radioButton.Name == "GeneralMenuButton" && GeneralPanel != null)
            {
                GeneralPanel.Visibility = Visibility.Visible;
            }
            else if (radioButton.Name == "QuickTasksMenuButton" && QuickTasksPanel != null)
            {
                QuickTasksPanel.Visibility = Visibility.Visible;
            }
            else if (radioButton.Name == "DocQuickOpenMenuButton" && DocQuickOpenPanel != null)
            {
                DocQuickOpenPanel.Visibility = Visibility.Visible;
                LoadDocQuickOpenSettings();
            }
            else if (radioButton.Name == "UpdatesMenuButton" && UpdatesPanel != null)
            {
                UpdatesPanel.Visibility = Visibility.Visible;
            }
        }
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
        
        _settings.SetSettingsTransparency(e.NewValue);
        _ = _settings.SaveAsync();
        StatusText.Text = "Settings transparency updated";
        
        // Update this window's transparency in real-time
        var alpha = (byte)(e.NewValue * 255);
        var color = System.Windows.Media.Color.FromArgb(alpha, 0x18, 0x18, 0x18);
        RootBorder.Background = new System.Windows.Media.SolidColorBrush(color);
        
        // Sync linked sliders
        if (_settings.GetSettingsTransparencyLinked())
        {
            SyncLinkedSliders(e.NewValue);
        }
        
        // Notify windows to update their transparency
        _onTransparencyChanged?.Invoke();
    }

    private void OverlayTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSliders || _settings == null) return;
        
        _settings.SetOverlayTransparency(e.NewValue);
        _ = _settings.SaveAsync();
        StatusText.Text = "Overlay transparency updated";
        
        // Sync linked sliders
        if (_settings.GetOverlayTransparencyLinked())
        {
            SyncLinkedSliders(e.NewValue);
        }
        
        // Notify windows to update their transparency
        _onTransparencyChanged?.Invoke();
    }

    private void WidgetLauncherTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSliders || _settings == null) return;
        
        _settings.SetWidgetLauncherTransparency(e.NewValue);
        _ = _settings.SaveAsync();
        StatusText.Text = "Widget launcher transparency updated";
        
        // Sync linked sliders
        if (_settings.GetLauncherTransparencyLinked())
        {
            SyncLinkedSliders(e.NewValue);
        }
        
        // Notify windows to update their transparency
        _onTransparencyChanged?.Invoke();
    }

    private void TimerWidgetTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSliders || _settings == null) return;
        
        _settings.SetTimerWidgetTransparency(e.NewValue);
        _ = _settings.SaveAsync();
        StatusText.Text = "Timer widget transparency updated";
        
        // Sync linked sliders
        if (_settings.GetTimerTransparencyLinked())
        {
            SyncLinkedSliders(e.NewValue);
        }
        
        // Notify windows to update their transparency
        _onTransparencyChanged?.Invoke();
    }

    private void LinkSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var isLinked = _settings.GetSettingsTransparencyLinked();
        _settings.SetSettingsTransparencyLinked(!isLinked);
        _ = _settings.SaveAsync();
        
        UpdateLinkButton(LinkSettingsButton, !isLinked);
        
        if (!isLinked)
        {
            SyncLinkedSliders(SettingsTransparencySlider.Value);
        }
        
        StatusText.Text = !isLinked ? "Settings transparency linked" : "Settings transparency unlinked";
    }

    private void LinkOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        var isLinked = _settings.GetOverlayTransparencyLinked();
        _settings.SetOverlayTransparencyLinked(!isLinked);
        _ = _settings.SaveAsync();
        
        UpdateLinkButton(LinkOverlayButton, !isLinked);
        
        if (!isLinked)
        {
            SyncLinkedSliders(OverlayTransparencySlider.Value);
        }
        
        StatusText.Text = !isLinked ? "Overlay transparency linked" : "Overlay transparency unlinked";
    }

    private void LinkLauncherButton_Click(object sender, RoutedEventArgs e)
    {
        var isLinked = _settings.GetLauncherTransparencyLinked();
        _settings.SetLauncherTransparencyLinked(!isLinked);
        _ = _settings.SaveAsync();
        
        UpdateLinkButton(LinkLauncherButton, !isLinked);
        
        if (!isLinked)
        {
            SyncLinkedSliders(WidgetLauncherTransparencySlider.Value);
        }
        
        StatusText.Text = !isLinked ? "Launcher transparency linked" : "Launcher transparency unlinked";
    }

    private void LinkTimerButton_Click(object sender, RoutedEventArgs e)
    {
        var isLinked = _settings.GetTimerTransparencyLinked();
        _settings.SetTimerTransparencyLinked(!isLinked);
        _ = _settings.SaveAsync();
        
        UpdateLinkButton(LinkTimerButton, !isLinked);
        
        if (!isLinked)
        {
            SyncLinkedSliders(TimerWidgetTransparencySlider.Value);
        }
        
        StatusText.Text = !isLinked ? "Timer transparency linked" : "Timer transparency unlinked";
    }

    private void SyncLinkedSliders(double value)
    {
        _isUpdatingSliders = true;
        
        if (_settings.GetSettingsTransparencyLinked())
        {
            SettingsTransparencySlider.Value = value;
            _settings.SetSettingsTransparency(value);
        }
        
        if (_settings.GetOverlayTransparencyLinked())
        {
            OverlayTransparencySlider.Value = value;
            _settings.SetOverlayTransparency(value);
        }
        
        if (_settings.GetLauncherTransparencyLinked())
        {
            WidgetLauncherTransparencySlider.Value = value;
            _settings.SetWidgetLauncherTransparency(value);
        }
        
        if (_settings.GetTimerTransparencyLinked())
        {
            TimerWidgetTransparencySlider.Value = value;
            _settings.SetTimerWidgetTransparency(value);
        }
        
        if (_settings.GetQuickTasksTransparencyLinked())
        {
            QuickTasksTransparencySlider.Value = value;
            _settings.SetQuickTasksWidgetTransparency(value);
        }
        
        if (_settings.GetDocTransparencyLinked())
        {
            DocTransparencySlider.Value = value;
            _settings.SetDocWidgetTransparency(value);
        }
        
        _isUpdatingSliders = false;
    }

    private void UpdateLinkButton(System.Windows.Controls.Button button, bool isLinked)
    {
        // Update the button's content to show linked/unlinked state
        var contentGrid = button.Content as System.Windows.Controls.Grid;
        if (contentGrid == null)
        {
            // Create a grid with icon if it doesn't exist
            contentGrid = new System.Windows.Controls.Grid();
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = isLinked ? "🔗" : "🔓",
                FontSize = 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            contentGrid.Children.Add(textBlock);
            button.Content = contentGrid;
        }
        else if (contentGrid.Children.Count > 0 && contentGrid.Children[0] is System.Windows.Controls.TextBlock tb)
        {
            tb.Text = isLinked ? "🔗" : "🔓";
        }
        
        // Update background color
        button.Background = isLinked 
            ? (System.Windows.Media.Brush)FindResource("PrimaryBrush") 
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
    }

    private void UpdateAllLinkButtons()
    {
        UpdateLinkButton(LinkSettingsButton, _settings.GetSettingsTransparencyLinked());
        UpdateLinkButton(LinkOverlayButton, _settings.GetOverlayTransparencyLinked());
        UpdateLinkButton(LinkLauncherButton, _settings.GetLauncherTransparencyLinked());
        UpdateLinkButton(LinkTimerButton, _settings.GetTimerTransparencyLinked());
        UpdateLinkButton(LinkQuickTasksButton, _settings.GetQuickTasksTransparencyLinked());
        UpdateLinkButton(LinkDocButton, _settings.GetDocTransparencyLinked());
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

    // ===== Quick Tasks Settings =====

    private void LoadQuickTasksSettings()
    {
        if (_taskService == null) return;

        _isLoadingQTSettings = true;
        try
        {
            var config = _taskService.Config;
            QT_ShowCompletedToggle.IsChecked = config.ShowCompletedTasks;
            QT_CompactModeToggle.IsChecked = config.CompactMode;
            QT_AutoCarryOverToggle.IsChecked = config.AutoCarryOver;
            QT_CompletedOpacitySlider.Value = config.CompletedOpacity;

            // Default priority
            QT_PriorityLow.IsChecked = config.DefaultPriority == "low";
            QT_PriorityNormal.IsChecked = config.DefaultPriority == "normal";
            QT_PriorityHigh.IsChecked = config.DefaultPriority == "high";

            // Sort mode
            QT_SortManual.IsChecked = config.SortBy == "manual";
            QT_SortPriority.IsChecked = config.SortBy == "priority";
            QT_SortCreated.IsChecked = config.SortBy == "created";

            // Categories
            RenderCategoriesList();
        }
        finally
        {
            _isLoadingQTSettings = false;
        }
    }

    private void RenderCategoriesList()
    {
        if (_taskService == null || QT_CategoriesList == null) return;

        QT_CategoriesList.Children.Clear();
        foreach (var category in _taskService.Config.Categories)
        {
            var cat = category;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new System.Windows.Controls.TextBlock
            {
                Text = cat,
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var removeBtn = new System.Windows.Controls.Button
            {
                Content = "✕",
                Width = 26,
                Height = 26,
                FontSize = 11,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x52, 0x52)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Apply rounded template
            var btnTemplate = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.Button));
            var btnBorder = new FrameworkElementFactory(typeof(Border));
            btnBorder.SetValue(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)));
            btnBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            var btnContent = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            btnContent.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            btnContent.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            btnBorder.AppendChild(btnContent);
            btnTemplate.VisualTree = btnBorder;
            removeBtn.Template = btnTemplate;
            removeBtn.Click += async (s, ev) =>
            {
                _taskService.Config.Categories.Remove(cat);
                await _taskService.ApplyConfigAsync();
                RenderCategoriesList();
                StatusText.Text = $"Category '{cat}' removed";
            };
            Grid.SetColumn(removeBtn, 1);
            row.Children.Add(removeBtn);

            QT_CategoriesList.Children.Add(row);
        }
    }

    private async void QT_ShowCompletedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        _taskService.Config.ShowCompletedTasks = QT_ShowCompletedToggle.IsChecked == true;
        await _taskService.ApplyConfigAsync();
        StatusText.Text = _taskService.Config.ShowCompletedTasks ? "Completed tasks visible" : "Completed tasks hidden";
    }

    private async void QT_CompactModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        _taskService.Config.CompactMode = QT_CompactModeToggle.IsChecked == true;
        await _taskService.ApplyConfigAsync();
        StatusText.Text = _taskService.Config.CompactMode ? "Compact mode enabled" : "Compact mode disabled";
    }

    private async void QT_AutoCarryOverToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        _taskService.Config.AutoCarryOver = QT_AutoCarryOverToggle.IsChecked == true;
        await _taskService.ApplyConfigAsync();
        StatusText.Text = _taskService.Config.AutoCarryOver ? "Auto carry-over enabled" : "Auto carry-over disabled";
    }

    private async void QT_CompletedOpacitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        _taskService.Config.CompletedOpacity = QT_CompletedOpacitySlider.Value;
        await _taskService.ApplyConfigAsync();
    }

    private async void QT_DefaultPriority_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        if (QT_PriorityLow.IsChecked == true) _taskService.Config.DefaultPriority = "low";
        else if (QT_PriorityHigh.IsChecked == true) _taskService.Config.DefaultPriority = "high";
        else _taskService.Config.DefaultPriority = "normal";
        await _taskService.ApplyConfigAsync();
        StatusText.Text = $"Default priority: {_taskService.Config.DefaultPriority}";
    }

    private async void QT_SortMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        if (QT_SortPriority.IsChecked == true) _taskService.Config.SortBy = "priority";
        else if (QT_SortCreated.IsChecked == true) _taskService.Config.SortBy = "created";
        else _taskService.Config.SortBy = "manual";
        await _taskService.ApplyConfigAsync();
        StatusText.Text = $"Sort order: {_taskService.Config.SortBy}";
    }

    private async void QT_AddCategory_Click(object sender, RoutedEventArgs e)
    {
        await AddNewCategory();
    }

    private async void QT_NewCategoryInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await AddNewCategory();
            e.Handled = true;
        }
    }

    private async Task AddNewCategory()
    {
        if (_taskService == null) return;
        var name = QT_NewCategoryInput.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (_taskService.Config.Categories.Contains(name)) return;

        _taskService.Config.Categories.Add(name);
        await _taskService.ApplyConfigAsync();
        QT_NewCategoryInput.Text = "";
        RenderCategoriesList();
        StatusText.Text = $"Category '{name}' added";
    }

    // ========== Doc Quick Open Settings ==========

    private void LoadDocQuickOpenSettings()
    {
        if (_docService == null) return;
        _isLoadingDQSettings = true;
        try
        {
            var cfg = _docService.Config;
            DQ_ShowFileSizeToggle.IsChecked = cfg.ShowFileSize;
            DQ_ShowDateModifiedToggle.IsChecked = cfg.ShowDateModified;
            DQ_ShowFileExtToggle.IsChecked = cfg.ShowFileExtension;
            DQ_CompactModeToggle.IsChecked = cfg.CompactMode;
            DQ_MaxDepthSlider.Value = cfg.MaxDepth;
            DQ_MaxDepthValue.Text = cfg.MaxDepth.ToString();
            DQ_MaxFilesSlider.Value = cfg.MaxFiles;
            DQ_MaxFilesValue.Text = cfg.MaxFiles.ToString();
            DQ_ExtensionsInput.Text = string.Join(", ", cfg.Extensions);
            DQ_AutoOpenToggle.IsChecked = cfg.AutoOpenLastProject;
            DQ_RecentCountSlider.Value = cfg.RecentFilesCount;
            DQ_RecentCountValue.Text = cfg.RecentFilesCount.ToString();

            // Sort radio
            switch (cfg.SortBy)
            {
                case "date": DQ_SortDate.IsChecked = true; break;
                case "type": DQ_SortType.IsChecked = true; break;
                case "size": DQ_SortSize.IsChecked = true; break;
                default: DQ_SortName.IsChecked = true; break;
            }

            // Group radio
            switch (cfg.GroupBy)
            {
                case "category": DQ_GroupCategory.IsChecked = true; break;
                case "extension": DQ_GroupExt.IsChecked = true; break;
                case "subfolder": DQ_GroupSubfolder.IsChecked = true; break;
                default: DQ_GroupNone.IsChecked = true; break;
            }

            // Transparency is now loaded in the Appearance tab
        }
        finally
        {
            _isLoadingDQSettings = false;
        }
    }

    private async void DQ_ShowFileSizeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        _docService.Config.ShowFileSize = DQ_ShowFileSizeToggle.IsChecked == true;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Show file size: {_docService.Config.ShowFileSize}";
    }

    private async void DQ_ShowDateModifiedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        _docService.Config.ShowDateModified = DQ_ShowDateModifiedToggle.IsChecked == true;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Show date modified: {_docService.Config.ShowDateModified}";
    }

    private async void DQ_ShowFileExtToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        _docService.Config.ShowFileExtension = DQ_ShowFileExtToggle.IsChecked == true;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Show file extension: {_docService.Config.ShowFileExtension}";
    }

    private async void DQ_CompactModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        _docService.Config.CompactMode = DQ_CompactModeToggle.IsChecked == true;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Compact mode: {_docService.Config.CompactMode}";
    }

    private async void DQ_MaxDepthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var val = (int)DQ_MaxDepthSlider.Value;
        DQ_MaxDepthValue.Text = val.ToString();
        _docService.Config.MaxDepth = val;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Max scan depth: {val}";
    }

    private async void DQ_MaxFilesSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var val = (int)DQ_MaxFilesSlider.Value;
        DQ_MaxFilesValue.Text = val.ToString();
        _docService.Config.MaxFiles = val;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Max files: {val}";
    }

    private async void DQ_ExtensionsInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var text = DQ_ExtensionsInput.Text;
        var exts = text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(x => x.Trim().TrimStart('.').ToLowerInvariant())
                       .Where(x => !string.IsNullOrEmpty(x))
                       .Distinct()
                       .ToList();
        _docService.Config.Extensions = exts;
        DQ_ExtensionsInput.Text = string.Join(", ", exts);
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"File extensions updated ({exts.Count} types)";
    }

    private async void DQ_SortChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        if (DQ_SortDate.IsChecked == true) _docService.Config.SortBy = "date";
        else if (DQ_SortType.IsChecked == true) _docService.Config.SortBy = "type";
        else if (DQ_SortSize.IsChecked == true) _docService.Config.SortBy = "size";
        else _docService.Config.SortBy = "name";
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Sort order: {_docService.Config.SortBy}";
    }

    private async void DQ_GroupChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        if (DQ_GroupCategory.IsChecked == true) _docService.Config.GroupBy = "category";
        else if (DQ_GroupExt.IsChecked == true) _docService.Config.GroupBy = "extension";
        else if (DQ_GroupSubfolder.IsChecked == true) _docService.Config.GroupBy = "subfolder";
        else _docService.Config.GroupBy = "none";
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Group by: {_docService.Config.GroupBy}";
    }

    private async void DQ_AutoOpenToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        _docService.Config.AutoOpenLastProject = DQ_AutoOpenToggle.IsChecked == true;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Remember last project: {_docService.Config.AutoOpenLastProject}";
    }

    private async void DQ_RecentCountSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var val = (int)DQ_RecentCountSlider.Value;
        DQ_RecentCountValue.Text = val.ToString();
        _docService.Config.RecentFilesCount = val;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Recent files count: {val}";
    }


    // ===== Appearance Tab - Quick Tasks & Doc Transparency =====

    private void QuickTasksTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSliders || _settings == null) return;
        
        _settings.SetQuickTasksWidgetTransparency(e.NewValue);
        _ = _settings.SaveAsync();
        StatusText.Text = "Quick Tasks widget transparency updated";
        
        if (_settings.GetQuickTasksTransparencyLinked())
        {
            SyncLinkedSliders(e.NewValue);
        }
        
        _onTransparencyChanged?.Invoke();
    }

    private void DocTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSliders || _settings == null) return;
        
        _settings.SetDocWidgetTransparency(e.NewValue);
        _ = _settings.SaveAsync();
        StatusText.Text = "Doc Quick Open widget transparency updated";
        
        if (_settings.GetDocTransparencyLinked())
        {
            SyncLinkedSliders(e.NewValue);
        }
        
        _onTransparencyChanged?.Invoke();
    }

    private void LinkQuickTasksButton_Click(object sender, RoutedEventArgs e)
    {
        var isLinked = _settings.GetQuickTasksTransparencyLinked();
        _settings.SetQuickTasksTransparencyLinked(!isLinked);
        _ = _settings.SaveAsync();
        
        UpdateLinkButton(LinkQuickTasksButton, !isLinked);
        
        if (!isLinked)
        {
            SyncLinkedSliders(QuickTasksTransparencySlider.Value);
        }
        
        StatusText.Text = !isLinked ? "Quick Tasks transparency linked" : "Quick Tasks transparency unlinked";
    }

    private void LinkDocButton_Click(object sender, RoutedEventArgs e)
    {
        var isLinked = _settings.GetDocTransparencyLinked();
        _settings.SetDocTransparencyLinked(!isLinked);
        _ = _settings.SaveAsync();
        
        UpdateLinkButton(LinkDocButton, !isLinked);
        
        if (!isLinked)
        {
            SyncLinkedSliders(DocTransparencySlider.Value);
        }
        
        StatusText.Text = !isLinked ? "Doc transparency linked" : "Doc transparency unlinked";
    }

    // ===== General Tab - Widget Enable/Disable Toggles =====

    private void TimerWidgetEnabledToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = TimerWidgetEnabledToggle.IsChecked == true;
        _settings.SetTimerWidgetEnabled(enabled);
        _ = _settings.SaveAsync();
        _onTimerWidgetEnabledChanged?.Invoke();
        StatusText.Text = enabled ? "Timer widget enabled" : "Timer widget disabled";
    }

    private void QuickTasksWidgetEnabledToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = QuickTasksWidgetEnabledToggle.IsChecked == true;
        _settings.SetQuickTasksWidgetEnabled(enabled);
        _ = _settings.SaveAsync();
        _onQuickTasksWidgetEnabledChanged?.Invoke();
        StatusText.Text = enabled ? "Quick Tasks widget enabled" : "Quick Tasks widget disabled";
    }

    private void DocWidgetEnabledToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = DocWidgetEnabledToggle.IsChecked == true;
        _settings.SetDocWidgetEnabled(enabled);
        _ = _settings.SaveAsync();
        _onDocWidgetEnabledChanged?.Invoke();
        StatusText.Text = enabled ? "Doc Quick Open widget enabled" : "Doc Quick Open widget disabled";
    }

    private void SearchWidgetEnabledToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = SearchWidgetEnabledToggle.IsChecked == true;
        _settings.SetSearchWidgetEnabled(enabled);
        _ = _settings.SaveAsync();
        _onSearchWidgetEnabledChanged?.Invoke();
        StatusText.Text = enabled ? "Search widget enabled" : "Search widget disabled";
    }

    // ===== General Tab - Update Settings =====

    private void AutoUpdateCheckToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = AutoUpdateCheckToggle.IsChecked == true;
        _settings.SetAutoUpdateCheckEnabled(enabled);
        _ = _settings.SaveAsync();
        _onUpdateSettingsChanged?.Invoke();
        StatusText.Text = enabled ? "Auto update check enabled" : "Auto update check disabled";
    }

    private void AutoUpdateInstallToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = AutoUpdateInstallToggle.IsChecked == true;
        _settings.SetAutoUpdateInstallEnabled(enabled);
        _ = _settings.SaveAsync();
        StatusText.Text = enabled ? "Auto install enabled" : "Auto install disabled";
    }

    private void UpdateFrequencyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        if (UpdateFrequencyCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int minutes))
        {
            _settings.SetUpdateCheckFrequencyMinutes(minutes);
            _ = _settings.SaveAsync();
            _onUpdateSettingsChanged?.Invoke();
            StatusText.Text = $"Update check frequency set to {minutes / 60} hour(s)";
        }
    }

    private void LoadUpdateFrequencyCombo()
    {
        var currentMinutes = _settings.GetUpdateCheckFrequencyMinutes();
        for (int i = 0; i < UpdateFrequencyCombo.Items.Count; i++)
        {
            if (UpdateFrequencyCombo.Items[i] is System.Windows.Controls.ComboBoxItem item && 
                item.Tag is string tagStr && int.TryParse(tagStr, out int minutes) && minutes == currentMinutes)
            {
                UpdateFrequencyCombo.SelectedIndex = i;
                return;
            }
        }
        // Default to "Every 6 hours" if no match
        UpdateFrequencyCombo.SelectedIndex = 1;
    }
}
