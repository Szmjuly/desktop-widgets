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
    /// Get whether settings window transparency is linked to the group
    /// </summary>
    bool GetSettingsTransparencyLinked();

    /// <summary>
    /// Set whether settings window transparency is linked to the group
    /// </summary>
    void SetSettingsTransparencyLinked(bool linked);

    /// <summary>
    /// Get overlay window transparency (0.0 to 1.0)
    /// </summary>
    double GetOverlayTransparency();

    /// <summary>
    /// Set overlay window transparency
    /// </summary>
    void SetOverlayTransparency(double transparency);

    /// <summary>
    /// Get whether overlay transparency is linked to the group
    /// </summary>
    bool GetOverlayTransparencyLinked();

    /// <summary>
    /// Set whether overlay transparency is linked to the group
    /// </summary>
    void SetOverlayTransparencyLinked(bool linked);

    /// <summary>
    /// Get widget launcher transparency (0.0 to 1.0)
    /// </summary>
    double GetWidgetLauncherTransparency();

    /// <summary>
    /// Set widget launcher transparency
    /// </summary>
    void SetWidgetLauncherTransparency(double transparency);

    /// <summary>
    /// Get whether widget launcher transparency is linked to the group
    /// </summary>
    bool GetLauncherTransparencyLinked();

    /// <summary>
    /// Set whether widget launcher transparency is linked to the group
    /// </summary>
    void SetLauncherTransparencyLinked(bool linked);

    /// <summary>
    /// Get timer widget transparency (0.0 to 1.0)
    /// </summary>
    double GetTimerWidgetTransparency();

    /// <summary>
    /// Set timer widget transparency
    /// </summary>
    void SetTimerWidgetTransparency(double transparency);

    /// <summary>
    /// Get whether timer widget transparency is linked to the group
    /// </summary>
    bool GetTimerTransparencyLinked();

    /// <summary>
    /// Set whether timer widget transparency is linked to the group
    /// </summary>
    void SetTimerTransparencyLinked(bool linked);

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
    /// Get saved search overlay visibility state
    /// </summary>
    bool GetSearchOverlayVisible();

    /// <summary>
    /// Set search overlay visibility state
    /// </summary>
    void SetSearchOverlayVisible(bool visible);

    /// <summary>
    /// Get saved widget launcher visibility state
    /// </summary>
    bool GetWidgetLauncherVisible();

    /// <summary>
    /// Set widget launcher visibility state
    /// </summary>
    void SetWidgetLauncherVisible(bool visible);

    /// <summary>
    /// Get quick tasks widget transparency (0.0 to 1.0)
    /// </summary>
    double GetQuickTasksWidgetTransparency();

    /// <summary>
    /// Set quick tasks widget transparency
    /// </summary>
    void SetQuickTasksWidgetTransparency(double transparency);

    /// <summary>
    /// Get whether quick tasks widget transparency is linked to the group
    /// </summary>
    bool GetQuickTasksTransparencyLinked();

    /// <summary>
    /// Set whether quick tasks widget transparency is linked to the group
    /// </summary>
    void SetQuickTasksTransparencyLinked(bool linked);

    /// <summary>
    /// Get saved timer widget position
    /// </summary>
    (double? left, double? top) GetTimerWidgetPosition();

    /// <summary>
    /// Set timer widget position
    /// </summary>
    void SetTimerWidgetPosition(double left, double top);

    /// <summary>
    /// Get saved timer widget visibility state
    /// </summary>
    bool GetTimerWidgetVisible();

    /// <summary>
    /// Set timer widget visibility state
    /// </summary>
    void SetTimerWidgetVisible(bool visible);

    /// <summary>
    /// Get saved quick tasks widget position
    /// </summary>
    (double? left, double? top) GetQuickTasksWidgetPosition();

    /// <summary>
    /// Set quick tasks widget position
    /// </summary>
    void SetQuickTasksWidgetPosition(double left, double top);

    /// <summary>
    /// Get saved quick tasks widget visibility state
    /// </summary>
    bool GetQuickTasksWidgetVisible();

    /// <summary>
    /// Set quick tasks widget visibility state
    /// </summary>
    void SetQuickTasksWidgetVisible(bool visible);

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
