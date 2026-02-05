namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Service for managing application settings
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Get Q: drive path
    /// </summary>
    string GetQDrivePath();

    /// <summary>
    /// Set Q: drive path
    /// </summary>
    void SetQDrivePath(string path);

    /// <summary>
    /// Get whether Q drive is enabled for scanning
    /// </summary>
    bool GetQDriveEnabled();

    /// <summary>
    /// Set whether Q drive is enabled for scanning
    /// </summary>
    void SetQDriveEnabled(bool enabled);

    /// <summary>
    /// Get P: drive path
    /// </summary>
    string GetPDrivePath();

    /// <summary>
    /// Set P drive path
    /// </summary>
    void SetPDrivePath(string path);

    /// <summary>
    /// Get whether P drive is enabled for scanning
    /// </summary>
    bool GetPDriveEnabled();

    /// <summary>
    /// Set whether P drive is enabled for scanning
    /// </summary>
    void SetPDriveEnabled(bool enabled);

    /// <summary>
    /// Get scan interval in minutes
    /// </summary>
    int GetScanIntervalMinutes();

    /// <summary>
    /// Set scan interval in minutes
    /// </summary>
    void SetScanIntervalMinutes(int minutes);

    /// <summary>
    /// Get global hotkey configuration
    /// </summary>
    (int modifiers, int key) GetHotkey();

    /// <summary>
    /// Set global hotkey configuration
    /// </summary>
    void SetHotkey(int modifiers, int key);

    /// <summary>
    /// Get theme (Light or Dark)
    /// </summary>
    string GetTheme();

    /// <summary>
    /// Set theme
    /// </summary>
    void SetTheme(string theme);

    /// <summary>
    /// Get auto-start with Windows setting
    /// </summary>
    bool GetAutoStart();

    /// <summary>
    /// Set auto-start with Windows
    /// </summary>
    void SetAutoStart(bool enabled);

    /// <summary>
    /// Get settings window transparency (0.0 to 1.0)
    /// </summary>
    double GetSettingsTransparency();

    /// <summary>
    /// Set settings window transparency
    /// </summary>
    void SetSettingsTransparency(double transparency);

    /// <summary>
    /// Get overlay window transparency (0.0 to 1.0)
    /// </summary>
    double GetOverlayTransparency();

    /// <summary>
    /// Set overlay window transparency
    /// </summary>
    void SetOverlayTransparency(double transparency);

    /// <summary>
    /// Get whether transparency is linked between windows
    /// </summary>
    bool GetTransparencyLinked();

    /// <summary>
    /// Set whether transparency is linked between windows
    /// </summary>
    void SetTransparencyLinked(bool linked);

    /// <summary>
    /// Get widget launcher transparency (0.0 to 1.0)
    /// </summary>
    double GetWidgetLauncherTransparency();

    /// <summary>
    /// Set widget launcher transparency
    /// </summary>
    void SetWidgetLauncherTransparency(double transparency);

    /// <summary>
    /// Get timer widget transparency (0.0 to 1.0)
    /// </summary>
    double GetTimerWidgetTransparency();

    /// <summary>
    /// Set timer widget transparency
    /// </summary>
    void SetTimerWidgetTransparency(double transparency);

    /// <summary>
    /// Get notification duration in milliseconds
    /// </summary>
    int GetNotificationDurationMs();

    /// <summary>
    /// Set notification duration in milliseconds
    /// </summary>
    void SetNotificationDurationMs(int durationMs);

    /// <summary>
    /// Get whether Living Widgets Mode is enabled (draggable, snappable, pinnable)
    /// </summary>
    bool GetLivingWidgetsMode();

    /// <summary>
    /// Set whether Living Widgets Mode is enabled
    /// </summary>
    void SetLivingWidgetsMode(bool enabled);

    /// <summary>
    /// Get saved search overlay position for Living Widgets Mode
    /// </summary>
    (double? left, double? top) GetSearchOverlayPosition();

    /// <summary>
    /// Set search overlay position for Living Widgets Mode
    /// </summary>
    void SetSearchOverlayPosition(double left, double top);

    /// <summary>
    /// Get saved widget launcher position for Living Widgets Mode
    /// </summary>
    (double? left, double? top) GetWidgetLauncherPosition();

    /// <summary>
    /// Set widget launcher position for Living Widgets Mode
    /// </summary>
    void SetWidgetLauncherPosition(double left, double top);

    /// <summary>
    /// Get saved widget launcher visibility state
    /// </summary>
    bool GetWidgetLauncherVisible();

    /// <summary>
    /// Set widget launcher visibility state
    /// </summary>
    void SetWidgetLauncherVisible(bool visible);

    /// <summary>
    /// Get close shortcut configuration for closing widgets
    /// </summary>
    (int modifiers, int key) GetCloseShortcut();

    /// <summary>
    /// Set close shortcut configuration
    /// </summary>
    void SetCloseShortcut(int modifiers, int key);

    /// <summary>
    /// Save all settings to disk
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Load settings from disk
    /// </summary>
    Task LoadAsync();
}
