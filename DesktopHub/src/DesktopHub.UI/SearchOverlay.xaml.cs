using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using System.Windows.Automation;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Data;
using DesktopHub.Infrastructure.Scanning;
using DesktopHub.Infrastructure.Search;
using DesktopHub.Infrastructure.Settings;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class SearchOverlay : Window
{
    private readonly IProjectScanner _scanner;
    private readonly ISearchService _searchService;
    private readonly IDataStore _dataStore;
    private readonly ISettingsService _settings;
    private readonly TimerService _timerService;
    private GlobalHotkey? _hotkey;
    private TrayIcon? _trayIcon;
    private TimerOverlay? _timerOverlay;
    private WidgetLauncher? _widgetLauncher;
    private List<Project> _allProjects = new();
    private List<Project> _filteredProjects = new();
    private CancellationTokenSource? _searchCts;
    private List<string> _searchHistory = new();
    private bool _isResultsCollapsed = false;
    private bool _userManuallySizedResults = false;
    private bool _isTogglingViaHotkey = false;
    private DateTime _lastHotkeyPress = DateTime.MinValue;
    private System.Windows.Threading.DispatcherTimer? _deactivateTimer;
    private CancellationTokenSource? _ipcCts;
    private bool _isClosing = false;
    private bool _isDragging = false;
    private System.Windows.Point _dragStartPoint;

    public bool IsClosing => _isClosing;

    public SearchOverlay()
    {
        DebugLogger.Clear();
        DebugLogger.Log("SearchOverlay: Constructor starting");
        
        InitializeComponent();

        // Initialize services
        _scanner = new ProjectScanner();
        _searchService = new SearchService();
        _dataStore = new SqliteDataStore();
        _settings = new SettingsService();
        _timerService = new TimerService();

        // Setup transparency when window handle is available
        SourceInitialized += (s, e) =>
        {
            try
            {
                DebugLogger.Log("SearchOverlay: SourceInitialized - Setting up transparency");
                DebugLogger.Log($"  Window.Background before: {this.Background}");
                DebugLogger.Log($"  Window.AllowsTransparency: {this.AllowsTransparency}");
                DebugLogger.Log($"  Window.WindowStyle: {this.WindowStyle}");
                
                WindowBlur.SetupTransparency(this);
                
                // DO NOT apply rounded corners to window region - it clips the window and creates white edges
                // WindowBlur.ApplyRoundedCorners(this, 12);
                DebugLogger.Log("  Skipping window region rounding to avoid clipping");
                
                UpdateRootClip(12);
                
                DebugLogger.Log($"  Window.Background after: {this.Background}");
                DebugLogger.Log($"  RootBorder.Background: {RootBorder.Background}");
                DebugLogger.Log("SearchOverlay: Transparency setup complete");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"SearchOverlay: Transparency setup FAILED: {ex}");
                System.Windows.MessageBox.Show($"Transparency setup failed: {ex.Message}\n\nCheck Desktop for DesktopHub_Debug.log", "Debug", MessageBoxButton.OK);
            }
        };

        // Reapply rounded corners on resize
        SizeChanged += (s, e) =>
        {
            // DO NOT apply window region rounding - causes clipping
            // WindowBlur.ApplyRoundedCorners(this, 12);
            UpdateRootClip(12);
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Enable blur effect (window handle is now available)
            try
            {
                DebugLogger.Log("SearchOverlay: Window_Loaded - Enabling blur");
                DebugLogger.Log($"  Window dimensions: {this.ActualWidth}x{this.ActualHeight}");
                DebugLogger.Log($"  Window Width/Height props: {this.Width}x{this.Height}");
                DebugLogger.Log($"  RootBorder dimensions: {RootBorder.ActualWidth}x{RootBorder.ActualHeight}");
                DebugLogger.Log($"  RootBorder Width/Height props: {RootBorder.Width}x{RootBorder.Height}");
                DebugLogger.Log($"  RootBorder margin: {RootBorder.Margin}");
                DebugLogger.Log($"  RootBorder padding: {RootBorder.Padding}");
                DebugLogger.Log($"  RootBorder HAlign: {RootBorder.HorizontalAlignment}");
                DebugLogger.Log($"  RootBorder VAlign: {RootBorder.VerticalAlignment}");
                DebugLogger.Log($"  RootBorder DesiredSize: {RootBorder.DesiredSize}");
                DebugLogger.Log($"  RootBorder RenderSize: {RootBorder.RenderSize}");
                DebugLogger.Log($"  Window is visible: {this.IsVisible}");
                DebugLogger.Log($"  Window opacity: {this.Opacity}");
                DebugLogger.Log($"  Window Content: {this.Content}");
                DebugLogger.Log($"  Content is RootBorder: {ReferenceEquals(this.Content, RootBorder)}");
                
                // Check if RootBorder fills the window
                var widthDiff = this.ActualWidth - RootBorder.ActualWidth;
                var heightDiff = this.ActualHeight - RootBorder.ActualHeight;
                DebugLogger.Log($"  Size difference (Window - Border): {widthDiff}x{heightDiff}");
                if (widthDiff > 1 || heightDiff > 1)
                {
                    DebugLogger.Log("  WARNING: RootBorder does NOT fill window - white edges will show!");
                }
                
                // DISABLE blur - it's rendering as solid black on this Windows version
                DebugLogger.Log("  SKIPPING blur - testing raw transparency without blur");
                // WindowBlur.EnableBlur(this, useAcrylic: false);
                
                DebugLogger.Log("SearchOverlay: Blur enabled");
                DebugLogger.Log($"  Final Window.Background: {this.Background}");
                DebugLogger.Log($"  Final RootBorder.Background: {RootBorder.Background}");
                
                // Log opacity of all layers
                var rootBg = RootBorder.Background as System.Windows.Media.SolidColorBrush;
                if (rootBg != null)
                {
                    DebugLogger.Log($"  RootBorder background color: {rootBg.Color}");
                    DebugLogger.Log($"  RootBorder alpha: {rootBg.Color.A} ({rootBg.Color.A / 255.0 * 100:F1}%)");
                }
                
                // Check if we can actually see through
                DebugLogger.Log($"  Window.Opacity: {this.Opacity}");
                DebugLogger.Log($"  RootBorder.Opacity: {RootBorder.Opacity}");
                DebugLogger.Log($"  Window.IsVisible: {this.IsVisible}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"SearchOverlay: Blur FAILED: {ex}");
            }
            
            // Load settings
            await _settings.LoadAsync();

            // Initialize database
            try
            {
                DebugLogger.Log("SearchOverlay: Starting database initialization");
                await _dataStore.InitializeAsync();
                DebugLogger.Log("SearchOverlay: Database initialized successfully");
            }
            catch (Exception dbEx)
            {
                DebugLogger.Log($"SearchOverlay: DATABASE INITIALIZATION FAILED: {dbEx.Message}");
                DebugLogger.Log($"SearchOverlay: Exception type: {dbEx.GetType().Name}");
                DebugLogger.Log($"SearchOverlay: Stack trace: {dbEx.StackTrace}");
                if (dbEx.InnerException != null)
                {
                    DebugLogger.Log($"SearchOverlay: Inner exception: {dbEx.InnerException.Message}");
                    DebugLogger.Log($"SearchOverlay: Inner exception type: {dbEx.InnerException.GetType().Name}");
                }
                throw;
            }

            var (modifiers, key) = _settings.GetHotkey();
            var hotkeyLabel = FormatHotkey(modifiers, key);

            // Initialize system tray icon
            _trayIcon = new TrayIcon(this, hotkeyLabel, _settings);

            // Initialize widget launcher
            _widgetLauncher = new WidgetLauncher(_settings);
            _widgetLauncher.TimerWidgetRequested += OnTimerWidgetRequested;

            // Register global hotkey (Ctrl+Alt+Space by default)
            try
            {
                _hotkey = new GlobalHotkey(this, (uint)modifiers, (uint)key);
                _hotkey.HotkeyPressed += OnHotkeyPressed;
                _hotkey.ShouldSuppressHotkey = () => ShouldSuppressHotkey(modifiers, key);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to register hotkey ({hotkeyLabel}). You can still open the search from the tray icon.\n\n{ex.Message}",
                    "DesktopHub",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning
                );
            }

            // Hide window after core initialization
            HideOverlay();

            // Load projects in the background
            _ = LoadProjectsAsync();

            // Start background scan if needed
            _ = Task.Run(async () => await BackgroundScanAsync());

            // Start IPC listener for commands from second instances
            _ = Task.Run(() => StartIpcListener());
            
            // Initialize dragging mode based on Living Widgets Mode setting
            UpdateDraggingMode();
            
            // Restore saved widget positions if in Living Widgets Mode
            var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
            if (isLivingWidgetsMode)
            {
                var (overlayLeft, overlayTop) = _settings.GetSearchOverlayPosition();
                if (overlayLeft.HasValue && overlayTop.HasValue)
                {
                    this.Left = overlayLeft.Value;
                    this.Top = overlayTop.Value;
                    DebugLogger.Log($"Restored search overlay position: ({overlayLeft.Value}, {overlayTop.Value})");
                }
                
                var (launcherLeft, launcherTop) = _settings.GetWidgetLauncherPosition();
                if (launcherLeft.HasValue && launcherTop.HasValue && _widgetLauncher != null)
                {
                    _widgetLauncher.Left = launcherLeft.Value;
                    _widgetLauncher.Top = launcherTop.Value;
                    DebugLogger.Log($"Restored widget launcher position: ({launcherLeft.Value}, {launcherTop.Value})");
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to initialize: {ex.Message}",
                "DesktopHub",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        DebugLogger.LogSeparator("HOTKEY PRESSED");
        DebugLogger.LogHeader("Hotkey Press - Initial State");
        
        // Debounce rapid hotkey presses (prevent double-triggering)
        var now = DateTime.Now;
        var timeSinceLastPress = (now - _lastHotkeyPress).TotalMilliseconds;
        DebugLogger.LogVariable("Time since last press (ms)", timeSinceLastPress);
        
        if (timeSinceLastPress < 200)
        {
            DebugLogger.Log("OnHotkeyPressed: DEBOUNCED (too soon after last press)");
            return;
        }
        _lastHotkeyPress = now;
        
        var (modifiers, key) = _settings.GetHotkey();
        DebugLogger.LogVariable("Hotkey Modifiers", modifiers);
        DebugLogger.LogVariable("Hotkey Key", key);
        DebugLogger.LogVariable("Hotkey Formatted", FormatHotkey(modifiers, key));
        
        // Check if we should suppress based on current visibility
        bool isCurrentlyVisible = false;
        Dispatcher.Invoke(() => { isCurrentlyVisible = this.Visibility == Visibility.Visible; });
        DebugLogger.LogVariable("Window Currently Visible", isCurrentlyVisible);
        
        // Note: Suppression is now handled in GlobalHotkey.ShouldSuppressHotkey callback
        // This code path only executes if the hotkey was NOT suppressed
        DebugLogger.Log("OnHotkeyPressed: Hotkey was NOT suppressed, proceeding with toggle");
        
        Dispatcher.Invoke(() =>
        {
            DebugLogger.LogHeader("Dispatcher.Invoke - Beginning Toggle");
            DebugLogger.LogVariable("Window.Visibility (before toggle)", this.Visibility);
            DebugLogger.LogVariable("Window.IsActive (before toggle)", this.IsActive);
            DebugLogger.LogVariable("Window.IsFocused (before toggle)", this.IsFocused);
            
            _isTogglingViaHotkey = true;
            DebugLogger.LogVariable("_isTogglingViaHotkey", _isTogglingViaHotkey);
            
            // Cancel any pending deactivate timer
            if (_deactivateTimer != null)
            {
                DebugLogger.Log("OnHotkeyPressed: Stopping deactivate timer");
                _deactivateTimer.Stop();
            }
            
            var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
            DebugLogger.LogVariable("Living Widgets Mode", isLivingWidgetsMode);
            
            if (isLivingWidgetsMode)
            {
                // Living Widgets Mode: bring forward or send to back
                if (this.Visibility != Visibility.Visible)
                {
                    DebugLogger.Log("OnHotkeyPressed: Window hidden -> SHOWING overlay");
                    ShowOverlay();
                }
                else if (this.IsActive)
                {
                    // Window is visible and active -> send to back
                    DebugLogger.Log("OnHotkeyPressed: Window active -> SENDING TO BACK");
                    this.Topmost = false;
                    
                    // Use Win32 API to send window to bottom of Z-order
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0, 
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                    
                    // Also send widget launcher to back if visible
                    if (_widgetLauncher != null && _widgetLauncher.Visibility == Visibility.Visible)
                    {
                        var launcherHwnd = new System.Windows.Interop.WindowInteropHelper(_widgetLauncher).Handle;
                        NativeMethods.SetWindowPos(launcherHwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0, 
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                        DebugLogger.Log("OnHotkeyPressed: Sent widget launcher to back too");
                    }
                    
                    // Also send timer overlay to back if visible
                    if (_timerOverlay != null && _timerOverlay.Visibility == Visibility.Visible)
                    {
                        var timerHwnd = new System.Windows.Interop.WindowInteropHelper(_timerOverlay).Handle;
                        NativeMethods.SetWindowPos(timerHwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0, 
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                        DebugLogger.Log("OnHotkeyPressed: Sent timer overlay to back too");
                    }
                }
                else
                {
                    // Window is visible but not active -> bring to front
                    DebugLogger.Log("OnHotkeyPressed: Window not active -> BRINGING TO FRONT");
                    this.Activate();
                    SearchBox.Focus();
                    
                    // Also bring widget launcher forward if visible
                    if (_widgetLauncher != null && _widgetLauncher.Visibility == Visibility.Visible)
                    {
                        _widgetLauncher.Activate();
                        DebugLogger.Log("OnHotkeyPressed: Brought widget launcher forward too");
                    }
                    
                    // Also bring timer overlay forward if visible
                    if (_timerOverlay != null && _timerOverlay.Visibility == Visibility.Visible)
                    {
                        _timerOverlay.Activate();
                        DebugLogger.Log("OnHotkeyPressed: Brought timer overlay forward too");
                    }
                }
            }
            else
            {
                // Legacy overlay mode: show/hide behavior
                if (this.Visibility == Visibility.Visible)
                {
                    DebugLogger.Log("OnHotkeyPressed: Window visible -> HIDING overlay");
                    HideOverlay();
                }
                else
                {
                    DebugLogger.Log("OnHotkeyPressed: Window hidden -> SHOWING overlay");
                    ShowOverlay();
                }
            }
            
            // Reset toggle flag after a short delay
            DebugLogger.Log("OnHotkeyPressed: Scheduling _isTogglingViaHotkey reset (300ms delay)");
            Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() => 
            {
                _isTogglingViaHotkey = false;
                DebugLogger.Log("OnHotkeyPressed: Reset _isTogglingViaHotkey to false");
            }));
        });
    }

    private bool ShouldSuppressHotkey(int modifiers, int key)
    {
        DebugLogger.LogHeader("ShouldSuppressHotkey Called");
        DebugLogger.LogVariable("Modifiers", modifiers);
        DebugLogger.LogVariable("Key", key);
        DebugLogger.LogVariable("Hotkey", FormatHotkey(modifiers, key));
        DebugLogger.LogVariable("Window.Visibility", this.Visibility);
        
        bool isCurrentlyVisible = this.Visibility == Visibility.Visible;
        
        // If overlay is visible, DON'T suppress - let the hotkey close it
        if (isCurrentlyVisible)
        {
            DebugLogger.Log("ShouldSuppressHotkey: NOT suppressing - overlay is visible, allow toggle to close");
            return false;
        }
        
        // If overlay is not visible, check if we should suppress due to text field focus
        var result = ShouldSuppressHotkeyForTyping(modifiers, key, isCurrentlyVisible);
        DebugLogger.LogVariable("ShouldSuppressHotkeyForTyping returned", result);
        return result;
    }
    
    private static bool ShouldSuppressHotkeyForTyping(int modifiers, int key, bool isCurrentlyVisible)
    {
        DebugLogger.LogHeader("ShouldSuppressHotkeyForTyping Called");
        DebugLogger.LogVariable("modifiers", modifiers);
        DebugLogger.LogVariable("key", key);
        DebugLogger.LogVariable("isCurrentlyVisible", isCurrentlyVisible);
        
        // When opening the overlay (not currently visible), be permissive - only suppress for clear text input scenarios
        // When closing the overlay (currently visible), allow it - user intentionally pressed hotkey
        
        // Only check text field focus when opening the overlay
        if (!isCurrentlyVisible)
        {
            DebugLogger.Log("ShouldSuppressHotkeyForTyping: Overlay NOT visible, checking for text field focus...");
            try
            {
                var focused = AutomationElement.FocusedElement;
                DebugLogger.LogVariable("AutomationElement.FocusedElement", focused != null ? "NOT NULL" : "NULL");
                
                if (focused == null)
                {
                    DebugLogger.Log("ShouldSuppressHotkeyForTyping: No focused element, NOT suppressing");
                    return false;
                }

                var controlType = focused.Current.ControlType;
                DebugLogger.LogVariable("Focused ControlType", controlType.ProgrammaticName);
                DebugLogger.LogVariable("Focused Name", focused.Current.Name);
                DebugLogger.LogVariable("Focused ClassName", focused.Current.ClassName);
                
                // Suppress only for clear text editing controls
                if (controlType == ControlType.Edit || controlType == ControlType.Document)
                {
                    DebugLogger.Log($"ShouldSuppressHotkeyForTyping: SUPPRESSING - text control detected: {controlType.ProgrammaticName}");
                    return true;
                }

                // Check for editable combo boxes
                if (controlType == ControlType.ComboBox)
                {
                    DebugLogger.Log("ShouldSuppressHotkeyForTyping: ComboBox detected, checking if editable...");
                    if (focused.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj))
                    {
                        var valuePattern = (ValuePattern)valuePatternObj;
                        var isReadOnly = valuePattern.Current.IsReadOnly;
                        DebugLogger.LogVariable("ComboBox.IsReadOnly", isReadOnly);
                        
                        if (!isReadOnly)
                        {
                            DebugLogger.Log("ShouldSuppressHotkeyForTyping: SUPPRESSING - editable ComboBox detected");
                            return true;
                        }
                    }
                }

                // Check for other editable text patterns
                DebugLogger.Log("ShouldSuppressHotkeyForTyping: Checking for ValuePattern...");
                if (focused.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj))
                {
                    var value = (ValuePattern)valueObj;
                    var isReadOnly = value.Current.IsReadOnly;
                    DebugLogger.LogVariable("ValuePattern.IsReadOnly", isReadOnly);
                    
                    if (!isReadOnly)
                    {
                        // Also check if it's a text-capable control
                        var hasTextPattern = focused.TryGetCurrentPattern(TextPattern.Pattern, out _);
                        DebugLogger.LogVariable("Has TextPattern", hasTextPattern);
                        
                        if (hasTextPattern)
                        {
                            DebugLogger.Log("ShouldSuppressHotkeyForTyping: SUPPRESSING - editable text pattern detected");
                            return true;
                        }
                    }
                }
                
                DebugLogger.Log("ShouldSuppressHotkeyForTyping: No text input detected, NOT suppressing");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"ShouldSuppressHotkeyForTyping: UIAutomation EXCEPTION: {ex.GetType().Name}");
                DebugLogger.Log($"ShouldSuppressHotkeyForTyping: Exception Message: {ex.Message}");
                // If UIAutomation fails (especially RPC_E_CANTCALLOUT_ININPUTSYNCCALL), 
                // we can't determine focus state. Since hotkey is input-synchronous and we're 
                // being called FROM the hotkey handler, we should be PERMISSIVE and allow it.
                // The exception means we're in a timing-sensitive input context, which suggests
                // the user is actively trying to trigger the hotkey, not typing in a text field.
                // If they were typing, the hotkey would be suppressed by the first check 
                // (overlay's own SearchBox) or wouldn't fire at all (normal text input absorbs keys).
                DebugLogger.Log("ShouldSuppressHotkeyForTyping: UIAutomation unavailable, defaulting to ALLOW (permissive)");
                return false;
            }
        }
        else
        {
            DebugLogger.Log("ShouldSuppressHotkeyForTyping: Overlay IS visible, NOT checking text fields (user wants to close)");
        }

        DebugLogger.Log("ShouldSuppressHotkeyForTyping: Returning FALSE (not suppressing)");
        return false;
    }

    private void ShowOverlay()
    {
        DebugLogger.LogSeparator("SHOW OVERLAY CALLED");
        DebugLogger.LogHeader("Initial State");
        DebugLogger.LogVariable("Window.Visibility", this.Visibility);
        DebugLogger.LogVariable("Window.IsVisible", this.IsVisible);
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("Window.Opacity", this.Opacity);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocusWithin", SearchBox.IsKeyboardFocusWithin);
        DebugLogger.LogVariable("_isTogglingViaHotkey", _isTogglingViaHotkey);
        DebugLogger.LogVariable("_isClosing", _isClosing);
        
        // Cancel any pending deactivate timer to prevent race conditions
        if (_deactivateTimer != null)
        {
            DebugLogger.Log("ShowOverlay: Cancelling pending deactivate timer");
            _deactivateTimer.Stop();
            _deactivateTimer = null;
        }
        
        // Don't show if window is closing
        if (_isClosing)
        {
            DebugLogger.Log("ShowOverlay: IGNORING - window is closing");
            return;
        }
        
        // Reset closing state when showing overlay (app is starting up or user action)
        _isClosing = false;
        
        ApplyDynamicTinting();
        
        // Reset manual toggle flag on new open
        _userManuallySizedResults = false;
        
        // Start with results collapsed
        _isResultsCollapsed = true;
        ResultsContainer.Visibility = Visibility.Collapsed;
        CollapseIconRotation.Angle = -90;
        this.Height = 140; // Collapsed height
        
        DebugLogger.LogHeader("Positioning Window");
        // Only reposition if Living Widgets Mode is disabled (legacy overlay mode)
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        if (!isLivingWidgetsMode)
        {
            PositionOnMouseScreen();
        }
        DebugLogger.LogVariable("Window.Left", this.Left);
        DebugLogger.LogVariable("Window.Top", this.Top);
        DebugLogger.LogVariable("Living Widgets Mode", isLivingWidgetsMode);
        
        DebugLogger.LogHeader("Making Window Visible");
        this.Visibility = Visibility.Visible;
        this.Opacity = 1;
        
        // Show widget launcher next to search overlay
        if (_widgetLauncher != null)
        {
            // Only auto-attach in Legacy mode; in Living Widgets Mode, remember position
            if (!isLivingWidgetsMode)
            {
                var windowWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
                _widgetLauncher.Left = this.Left + windowWidth + 12;
                _widgetLauncher.Top = this.Top;
            }
            
            _widgetLauncher.Visibility = Visibility.Visible;
        }
        
        // Timer overlay is now independent - don't auto-show/hide with search overlay
        
        DebugLogger.LogHeader("Calling Window.Activate()");
        var activateResult = this.Activate();
        DebugLogger.LogVariable("Activate() returned", activateResult);
        DebugLogger.LogVariable("Window.IsActive after Activate()", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused after Activate()", this.IsFocused);
        
        // Clear SearchBox first to prevent any keyboard events from adding characters
        DebugLogger.LogHeader("Clearing SearchBox");
        SearchBox.Clear();
        DebugLogger.LogVariable("SearchBox.Text after Clear()", SearchBox.Text);
        
        // Delay focus to ensure hotkey keyboard events are fully processed/blocked
        // Use DispatcherPriority.Input to run after all input events are processed
        DebugLogger.LogHeader("Scheduling Focus to SearchBox (DispatcherPriority.Input)");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            DebugLogger.LogHeader("Focus Callback Executing");
            DebugLogger.LogVariable("Window.IsActive (in callback)", this.IsActive);
            DebugLogger.LogVariable("Window.IsFocused (in callback)", this.IsFocused);
            DebugLogger.LogVariable("SearchBox.IsFocused (before Focus())", SearchBox.IsFocused);
            
            var focusResult = SearchBox.Focus();
            DebugLogger.LogVariable("SearchBox.Focus() returned", focusResult);
            DebugLogger.LogVariable("SearchBox.IsFocused (after Focus())", SearchBox.IsFocused);
            DebugLogger.LogVariable("SearchBox.IsKeyboardFocused (after Focus())", SearchBox.IsKeyboardFocused);
            DebugLogger.LogVariable("SearchBox.IsKeyboardFocusWithin (after Focus())", SearchBox.IsKeyboardFocusWithin);
            
            SearchBox.SelectAll();
            
            // Double-check and clear any text that appeared (safety measure)
            Task.Delay(50).ContinueWith(_ => Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(SearchBox.Text))
                {
                    DebugLogger.Log($"ShowOverlay: Clearing unexpected text in SearchBox: '{SearchBox.Text}'");
                    SearchBox.Clear();
                }
                
                DebugLogger.LogHeader("Final Focus State (after 50ms delay)");
                DebugLogger.LogVariable("Window.IsActive", this.IsActive);
                DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
                DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
                DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
                DebugLogger.LogVariable("SearchBox.IsKeyboardFocusWithin", SearchBox.IsKeyboardFocusWithin);
                DebugLogger.LogVariable("SearchBox.Text", SearchBox.Text);
            }));
        }), System.Windows.Threading.DispatcherPriority.Input);
        
        // Load all projects filtered by year
        LoadAllProjects();
        
        // Show history if search is blank
        UpdateHistoryVisibility();
        DebugLogger.Log("ShowOverlay: Returning from method");
    }

    private void PositionOnMouseScreen()
    {
        try
        {
            // Get current mouse position
            var mousePos = System.Windows.Forms.Cursor.Position;
            
            // Get the screen containing the mouse cursor
            var screen = Screen.FromPoint(mousePos);
            
            // Position at top-center of screen with margin
            var workingArea = screen.WorkingArea;
            
            // Use ActualWidth if available, otherwise use Width property
            var overlayWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
            
            // Calculate total width including widget launcher (180px) and gap (12px)
            var widgetLauncherWidth = _widgetLauncher != null ? 180.0 : 0.0;
            var gap = _widgetLauncher != null ? 12.0 : 0.0;
            var totalWidth = overlayWidth + gap + widgetLauncherWidth;
            
            DebugLogger.Log($"SearchOverlay: Positioning - WorkArea({workingArea.Left},{workingArea.Top},{workingArea.Width}x{workingArea.Height}), OverlayWidth={overlayWidth}, TotalWidth={totalWidth}");
            
            // Center the combined group on screen
            var groupLeft = workingArea.Left + (workingArea.Width - totalWidth) / 2.0;
            this.Left = groupLeft;
            this.Top = workingArea.Top + 80; // 80px from top edge
            
            DebugLogger.Log($"SearchOverlay: Positioned at ({this.Left}, {this.Top}), Mouse at ({mousePos.X}, {mousePos.Y})");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SearchOverlay: PositionOnMouseScreen failed: {ex.Message}, using default positioning");
            // Fallback to top of primary screen
            var primaryScreen = Screen.PrimaryScreen;
            if (primaryScreen != null)
            {
                var workingArea = primaryScreen.WorkingArea;
                var overlayWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
                var widgetLauncherWidth = _widgetLauncher != null ? 180.0 : 0.0;
                var gap = _widgetLauncher != null ? 12.0 : 0.0;
                var totalWidth = overlayWidth + gap + widgetLauncherWidth;
                var groupLeft = workingArea.Left + (workingArea.Width - totalWidth) / 2.0;
                this.Left = groupLeft;
                this.Top = workingArea.Top + 80;
            }
        }
    }

    private void ApplyDynamicTinting()
    {
        try
        {
            // Get overlay transparency from settings
            var transparency = _settings.GetOverlayTransparency();
            var alpha = (byte)(transparency * 255);
            
            // Apple Spotlight style: neutral colors, no accent tinting
            RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x18, 0x18, 0x18));
            RootBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0x3A, 0x3A, 0x3A));
            
            GlassOverlay.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            GlassOverlay.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SearchOverlay: ApplyDynamicTinting failed: {ex.Message}");
        }
    }
    
    private void UpdateHistoryVisibility()
    {
        var isSearchBlank = string.IsNullOrWhiteSpace(SearchBox.Text);
        var hasHistory = _searchHistory.Any();
        var hasResults = ResultsList?.Items.Count > 0;
        
        // Always show container if there's history OR results to collapse
        var shouldShowContainer = hasHistory || hasResults;
        
        if (shouldShowContainer)
        {
            HistoryAndCollapseContainer.Visibility = Visibility.Visible;
            
            // Show actual history if available, otherwise show placeholder text
            if (hasHistory)
            {
                // Show history pills
                HistoryScrollViewer.Visibility = Visibility.Visible;
                HistoryPlaceholder.Visibility = Visibility.Collapsed;
                HorizontalHistoryList.ItemsSource = _searchHistory.Take(5).ToList();
            }
            else
            {
                // Show single random placeholder as background text
                HistoryScrollViewer.Visibility = Visibility.Collapsed;
                HistoryPlaceholder.Visibility = Visibility.Visible;
                HistoryPlaceholder.Text = GetRandomHistoryPlaceholder();
            }
        }
        else
        {
            HistoryAndCollapseContainer.Visibility = Visibility.Collapsed;
        }
        
        DebugLogger.Log($"UpdateHistoryVisibility: isSearchBlank={isSearchBlank}, hasHistory={hasHistory}, hasResults={hasResults}, containerVisible={HistoryAndCollapseContainer.Visibility}");
    }
    
    private string GetRandomHistoryPlaceholder()
    {
        var placeholders = new[]
        {
            "No history yet...",
            "Nothing here",
            "Start searching!",
            "No recent searches",
            "Search history empty"
        };
        var random = new Random();
        return placeholders[random.Next(placeholders.Length)];
    }

    private void LoadAllProjects()
    {
        try
        {
            DebugLogger.Log($"LoadAllProjects: Starting, total projects: {_allProjects.Count}");
            ShowLoading(true);
            
            // Get selected filters
            var selectedYear = YearFilter.SelectedItem?.ToString();
            var selectedLocation = DriveLocationFilter.SelectedItem?.ToString();
            
            // Start with all projects, but filter by enabled drives first
            _filteredProjects = _allProjects.Where(p => 
            {
                if (p.DriveLocation == "Q") return _settings.GetQDriveEnabled();
                if (p.DriveLocation == "P") return _settings.GetPDriveEnabled();
                return false; // Unknown drive, exclude
            }).ToList();
            
            DebugLogger.Log($"LoadAllProjects: After drive filter: {_filteredProjects.Count} projects (Q enabled: {_settings.GetQDriveEnabled()}, P enabled: {_settings.GetPDriveEnabled()})");
            
            // Apply year filter
            if (selectedYear != "All Years" && !string.IsNullOrEmpty(selectedYear))
            {
                _filteredProjects = _filteredProjects.Where(p => p.Year == selectedYear).ToList();
            }
            
            // Apply drive location filter
            if (!string.IsNullOrEmpty(selectedLocation) && selectedLocation != "All Locations")
            {
                var driveFilter = selectedLocation.Contains("Florida") ? "Q" : "P";
                _filteredProjects = _filteredProjects.Where(p => p.DriveLocation == driveFilter).ToList();
            }
            
            DebugLogger.Log($"LoadAllProjects: Final filtered count: {_filteredProjects.Count} projects for year {selectedYear}, location {selectedLocation}");
            ResultsList.ItemsSource = _filteredProjects;
            UpdateResultsHeader();
            UpdateHistoryVisibility();
            ShowLoading(false);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SearchOverlay: LoadAllProjects error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void UpdateResultsHeader()
    {
        var count = ResultsList.Items.Count;
        var selectedYear = YearFilter.SelectedItem?.ToString();
        
        if (selectedYear == "All Years" || string.IsNullOrEmpty(selectedYear))
        {
            ResultsHeaderText.Text = $"Projects ({count})";
        }
        else
        {
            ResultsHeaderText.Text = $"{selectedYear} Projects ({count})";
        }
    }

    private void ToggleResults_Click(object sender, MouseButtonEventArgs e)
    {
        _isResultsCollapsed = !_isResultsCollapsed;
        _userManuallySizedResults = true; // User manually toggled
        
        DebugLogger.Log($"ToggleResults_Click: Toggled to {(_isResultsCollapsed ? "collapsed" : "expanded")}");
        
        if (_isResultsCollapsed)
        {
            // Collapse results - shrink window
            ResultsContainer.Visibility = Visibility.Collapsed;
            CollapseIconRotation.Angle = -90; // Rotate arrow to point right
            this.Height = 140; // Compact height for search bar and history pills only
        }
        else
        {
            // Expand results - restore full window height
            ResultsContainer.Visibility = Visibility.Visible;
            CollapseIconRotation.Angle = 0; // Arrow points down
            this.Height = 500; // Full height with results
        }
    }

    public void ShowFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            if (this.Visibility != Visibility.Visible)
            {
                ShowOverlay();
            }
            else
            {
                this.Activate();
                SearchBox.Focus();
            }
        });
    }

    private void HideOverlay()
    {
        DebugLogger.LogSeparator("HIDE OVERLAY CALLED");
        DebugLogger.LogHeader("Initial State");
        DebugLogger.LogVariable("Window.Visibility", this.Visibility);
        DebugLogger.LogVariable("Window.IsVisible", this.IsVisible);
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
        DebugLogger.LogVariable("_isTogglingViaHotkey", _isTogglingViaHotkey);
        
        DebugLogger.LogHeader("Clearing Focus from SearchBox");
        // CRITICAL: Move focus away from SearchBox to prevent stale focus state
        // When window is hidden, SearchBox retains logical focus but loses keyboard routing
        // This causes focus to break on next show - SearchBox.IsFocused=true but keyboard doesn't work
        // Keyboard.ClearFocus() only clears keyboard routing, not logical focus
        // Must explicitly move focus to another element (FocusTrap) to clear IsFocused
        FocusTrap.Focus();
        DebugLogger.LogVariable("After FocusTrap.Focus() - SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("After FocusTrap.Focus() - SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
        DebugLogger.LogVariable("After FocusTrap.Focus() - FocusTrap.IsFocused", FocusTrap.IsFocused);
        
        DebugLogger.LogHeader("Hiding Window");
        this.Visibility = Visibility.Hidden;
        this.Opacity = 0;
        
        // Hide widget launcher
        if (_widgetLauncher != null)
        {
            _widgetLauncher.Visibility = Visibility.Hidden;
        }
        
        // Timer overlay is now independent - don't auto-hide with search overlay
        
        DebugLogger.LogHeader("Clearing SearchBox and UI");
        SearchBox.Clear();
        ResultsList.ItemsSource = null;
        
        // Clear history pills to prevent them from reappearing
        HorizontalHistoryList.ItemsSource = null;
        HistoryAndCollapseContainer.Visibility = Visibility.Collapsed;
        
        DebugLogger.LogHeader("Final State");
        DebugLogger.LogVariable("Window.Visibility", this.Visibility);
        DebugLogger.LogVariable("Window.IsVisible", this.IsVisible);
        DebugLogger.LogVariable("SearchBox.IsFocused (final)", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused (final)", SearchBox.IsKeyboardFocused);
        DebugLogger.Log("HideOverlay: Returning from method");
    }

    private static string FormatHotkey(int modifiers, int key)
    {
        var parts = new List<string>();

        if ((modifiers & GlobalHotkey.MOD_CONTROL) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & GlobalHotkey.MOD_ALT) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & GlobalHotkey.MOD_SHIFT) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & GlobalHotkey.MOD_WIN) != 0)
        {
            parts.Add("Win");
        }

        var keyLabel = System.Windows.Input.KeyInterop.KeyFromVirtualKey(key);
        var keyText = keyLabel != System.Windows.Input.Key.None ? keyLabel.ToString() : $"0x{key:X}";
        parts.Add(keyText);

        return string.Join("+", parts);
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            ShowLoading(true);
            StatusText.Text = "Loading projects...";

            _allProjects = await _dataStore.GetAllProjectsAsync();

            // Populate year and drive location filters
            PopulateYearFilter();
            PopulateDriveLocationFilter();

            // Count only projects from enabled drives
            var enabledProjectsCount = _allProjects.Count(p => 
            {
                if (p.DriveLocation == "Q") return _settings.GetQDriveEnabled();
                if (p.DriveLocation == "P") return _settings.GetPDriveEnabled();
                return false;
            });

            StatusText.Text = $"{enabledProjectsCount} projects loaded";
            DebugLogger.Log($"LoadProjectsAsync: Total in DB: {_allProjects.Count}, From enabled drives: {enabledProjectsCount} (Q: {_settings.GetQDriveEnabled()}, P: {_settings.GetPDriveEnabled()})");
            ShowLoading(false);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading projects: {ex.Message}";
            ShowLoading(false);
        }
    }

    private void PopulateYearFilter()
    {
        var years = _allProjects
            .Select(p => p.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        years.Insert(0, "All Years");

        YearFilter.ItemsSource = years;
        YearFilter.SelectedIndex = 0;
    }

    private void PopulateDriveLocationFilter()
    {
        var locations = new List<string> { "All Locations" };
        
        // Only add enabled drives to the dropdown
        if (_settings.GetQDriveEnabled())
        {
            locations.Add("Florida (Q:)");
        }
        
        if (_settings.GetPDriveEnabled())
        {
            locations.Add("Connecticut (P:)");
        }
        
        DriveLocationFilter.ItemsSource = locations;
        DriveLocationFilter.SelectedIndex = 0;
    }

    private async Task BackgroundScanAsync()
    {
        try
        {
            // Check if we need to scan (based on last scan time)
            var lastScan = await _dataStore.GetLastScanTimeAsync();
            var scanInterval = TimeSpan.FromMinutes(_settings.GetScanIntervalMinutes());

            if (lastScan == null || DateTime.UtcNow - lastScan.Value > scanInterval)
            {
                var allScannedProjects = new List<Project>();

                // Scan Q: drive (Florida) - only if enabled
                if (_settings.GetQDriveEnabled())
                {
                    await Dispatcher.InvokeAsync(() => StatusText.Text = "Scanning Q: drive (Florida)...");
                    var qDrivePath = _settings.GetQDrivePath();
                    if (Directory.Exists(qDrivePath))
                    {
                        try
                        {
                            var qProjects = await _scanner.ScanProjectsAsync(qDrivePath, "Q", CancellationToken.None);
                            allScannedProjects.AddRange(qProjects);
                            DebugLogger.Log($"Q: drive scan completed: {qProjects.Count} projects found");
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"Q: drive scan error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    DebugLogger.Log("Q: drive scanning disabled - skipping");
                }

                // Scan P: drive (Connecticut) - only if enabled
                if (_settings.GetPDriveEnabled())
                {
                    await Dispatcher.InvokeAsync(() => StatusText.Text = "Scanning P: drive (Connecticut)...");
                    var pDrivePath = _settings.GetPDrivePath();
                    if (Directory.Exists(pDrivePath))
                    {
                        try
                        {
                            var pProjects = await _scanner.ScanProjectsAsync(pDrivePath, "P", CancellationToken.None);
                            allScannedProjects.AddRange(pProjects);
                            DebugLogger.Log($"P: drive scan completed: {pProjects.Count} projects found");
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"P: drive scan error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    DebugLogger.Log("P: drive scanning disabled - skipping");
                }

                // Update database with all scanned projects (Q and P drives are separate)
                await _dataStore.BatchUpsertProjectsAsync(allScannedProjects);
                await _dataStore.UpdateLastScanTimeAsync(DateTime.UtcNow);

                // Reload projects
                await Dispatcher.InvokeAsync(async () => await LoadProjectsAsync());
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = $"Scan error: {ex.Message}";
            });
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Cancel previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        var query = SearchBox.Text;

        if (string.IsNullOrWhiteSpace(query))
        {
            // Show all projects when search is blank
            LoadAllProjects();
            UpdateHistoryVisibility();
            
            // Auto-collapse when search cleared (only if user hasn't manually toggled)
            if (!_userManuallySizedResults && !_isResultsCollapsed)
            {
                _isResultsCollapsed = true;
                ResultsContainer.Visibility = Visibility.Collapsed;
                CollapseIconRotation.Angle = -90;
                this.Height = 140;
            }
            return;
        }

        // Update history visibility (hide horizontal pills when typing)
        UpdateHistoryVisibility();

        try
        {
            // Debounce search (wait 250ms for slower PCs)
            await Task.Delay(250, token);

            if (token.IsCancellationRequested)
                return;

            ShowLoading(true);

            // Apply filters before searching
            var selectedYear = YearFilter.SelectedItem?.ToString();
            var selectedLocation = DriveLocationFilter.SelectedItem?.ToString();
            
            // Start with all projects, but filter by enabled drives first
            var projectsToSearch = _allProjects.Where(p => 
            {
                if (p.DriveLocation == "Q") return _settings.GetQDriveEnabled();
                if (p.DriveLocation == "P") return _settings.GetPDriveEnabled();
                return false; // Unknown drive, exclude
            }).ToList();
            
            // Apply year filter
            if (selectedYear != "All Years" && !string.IsNullOrEmpty(selectedYear))
            {
                projectsToSearch = projectsToSearch.Where(p => p.Year == selectedYear).ToList();
            }
            
            // Apply drive location filter
            if (!string.IsNullOrEmpty(selectedLocation) && selectedLocation != "All Locations")
            {
                var driveFilter = selectedLocation.Contains("Florida") ? "Q" : "P";
                projectsToSearch = projectsToSearch.Where(p => p.DriveLocation == driveFilter).ToList();
            }

            // Search filtered projects
            var results = await _searchService.SearchAsync(query, projectsToSearch);

            if (token.IsCancellationRequested)
                return;

            // Update UI - batch operations to reduce overhead
            var projectViewModels = results.Select(r => new ProjectViewModel(r.Project)).ToList();
            ResultsList.ItemsSource = projectViewModels;

            if (results.Any())
            {
                ResultsList.SelectedIndex = 0;
                StatusText.Text = $"{results.Count} result{(results.Count == 1 ? "" : "s")} found";
                
                // Auto-expand when search has results (only if user hasn't manually toggled)
                if (!_userManuallySizedResults && _isResultsCollapsed)
                {
                    _isResultsCollapsed = false;
                    ResultsContainer.Visibility = Visibility.Visible;
                    CollapseIconRotation.Angle = 0;
                    this.Height = 500;
                }
                
                // History tracking removed - only track on actual project launch
            }
            else
            {
                StatusText.Text = "No results found";
            }

            UpdateResultsHeader();
            UpdateHistoryVisibility();
            ShowLoading(false);
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled, ignore
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SearchBox_TextChanged: Search error: {ex.Message}\n{ex.StackTrace}");
            StatusText.Text = $"Search error: {ex.Message}";
            ShowLoading(false);
        }
    }

    private List<Project> GetFilteredProjectsByYear()
    {
        var selectedYear = YearFilter.SelectedItem as string;
        if (string.IsNullOrEmpty(selectedYear) || selectedYear == "All Years")
        {
            return _allProjects;
        }

        return _allProjects.Where(p => p.Year == selectedYear).ToList();
    }

    private void AddToSearchHistory(string query)
    {
        // Remove if already exists
        _searchHistory.Remove(query);
        
        // Add to front
        _searchHistory.Insert(0, query);
        
        // Keep only last 25 to prevent excessive memory usage
        if (_searchHistory.Count > 25)
        {
            _searchHistory = _searchHistory.Take(25).ToList();
        }
    }

    private void YearFilter_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Reload projects with new year filter
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // Re-trigger search with new filter
            SearchBox_TextChanged(SearchBox, new TextChangedEventArgs(e.RoutedEvent, System.Windows.Controls.UndoAction.None));
        }
        else
        {
            // Reload all projects with new year filter
            LoadAllProjects();
        }
    }

    private void DriveLocationFilter_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Reload projects with new drive location filter
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // Re-trigger search with new filter
            SearchBox_TextChanged(SearchBox, new TextChangedEventArgs(e.RoutedEvent, System.Windows.Controls.UndoAction.None));
        }
        else
        {
            // Reload all projects with new drive location filter
            LoadAllProjects();
        }
    }

    private void WidgetLauncherToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_widgetLauncher != null)
        {
            if (_widgetLauncher.Visibility == Visibility.Visible)
            {
                _widgetLauncher.Visibility = Visibility.Hidden;
                DebugLogger.Log("Widget launcher hidden via toggle button");
            }
            else
            {
                _widgetLauncher.Visibility = Visibility.Visible;
                DebugLogger.Log("Widget launcher shown via toggle button");
            }
        }
    }
    
    private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border border && border.DataContext is string query)
        {
            SearchBox.Text = query;
        }
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        DebugLogger.LogHeader("SearchBox GotFocus");
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocusWithin", SearchBox.IsKeyboardFocusWithin);
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        DebugLogger.LogHeader("SearchBox LostFocus");
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
    }

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        DebugLogger.LogHeader("SearchBox PreviewKeyDown");
        DebugLogger.LogVariable("Key", e.Key);
        DebugLogger.LogVariable("SystemKey", e.SystemKey);
        DebugLogger.LogVariable("KeyStates", e.KeyStates);
        DebugLogger.LogVariable("IsDown", e.IsDown);
        DebugLogger.LogVariable("IsUp", e.IsUp);
        DebugLogger.LogVariable("IsRepeat", e.IsRepeat);
        DebugLogger.LogVariable("Keyboard.Modifiers", System.Windows.Input.Keyboard.Modifiers);
        DebugLogger.LogVariable("SearchBox.Text (before)", SearchBox.Text);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        DebugLogger.LogHeader("Window KeyDown");
        DebugLogger.LogVariable("Key", e.Key);
        DebugLogger.LogVariable("Handled", e.Handled);
        DebugLogger.LogVariable("Source", e.Source?.GetType().Name ?? "<null>");
        
        switch (e.Key)
        {
            case System.Windows.Input.Key.Escape:
                DebugLogger.Log("Window_KeyDown: Escape pressed -> Hiding overlay");
                HideOverlay();
                e.Handled = true;
                break;

            case System.Windows.Input.Key.Enter:
                DebugLogger.Log("Window_KeyDown: Enter pressed -> Opening selected project");
                OpenSelectedProject();
                e.Handled = true;
                break;

            case System.Windows.Input.Key.Down:
                if (ResultsList.Items.Count > 0)
                {
                    var newIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
                    DebugLogger.Log($"Window_KeyDown: Down pressed -> Selecting item {newIndex}");
                    ResultsList.SelectedIndex = newIndex;
                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                }
                e.Handled = true;
                break;

            case System.Windows.Input.Key.Up:
                if (ResultsList.Items.Count > 0)
                {
                    var newIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
                    DebugLogger.Log($"Window_KeyDown: Up pressed -> Selecting item {newIndex}");
                    ResultsList.SelectedIndex = newIndex;
                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                }
                e.Handled = true;
                break;

            case System.Windows.Input.Key.C when System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control:
                DebugLogger.Log("Window_KeyDown: Ctrl+C pressed -> Copying path");
                CopySelectedProjectPath();
                e.Handled = true;
                break;
        }
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Could show more details here
    }

    private void ResultsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenSelectedProject();
    }

    private void OpenSelectedProject()
    {
        if (ResultsList.SelectedItem is ProjectViewModel vm)
        {
            try
            {
                // Track search query when project is actually opened
                var query = SearchBox.Text;
                if (!string.IsNullOrWhiteSpace(query))
                {
                    AddToSearchHistory(query);
                }
                
                Process.Start("explorer.exe", vm.Path);
                
                // Only hide overlay if NOT in Living Widgets Mode (live widget mode keeps it open)
                var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
                if (!isLivingWidgetsMode)
                {
                    HideOverlay();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open project folder: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error
                );
            }
        }
    }

    private void CopySelectedProjectPath()
    {
        if (ResultsList.SelectedItem is ProjectViewModel vm)
        {
            try
            {
                System.Windows.Clipboard.SetText(vm.Path);
                StatusText.Text = "Path copied to clipboard";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to copy: {ex.Message}";
            }
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        DebugLogger.LogSeparator("WINDOW DEACTIVATED");
        DebugLogger.LogHeader("Window Lost Focus");
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("Window.Visibility", this.Visibility);
        DebugLogger.LogVariable("_isTogglingViaHotkey", _isTogglingViaHotkey);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
        
        // Don't auto-hide if Living Widgets Mode is enabled
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        DebugLogger.LogVariable("LivingWidgetsMode", isLivingWidgetsMode);
        if (isLivingWidgetsMode)
        {
            DebugLogger.Log("Window_Deactivated: IGNORING - Living Widgets Mode is enabled");
            return;
        }
        
        // Don't auto-hide if we're in the middle of a hotkey toggle
        if (_isTogglingViaHotkey)
        {
            DebugLogger.Log("Window_Deactivated: IGNORING - hotkey toggle in progress");
            return;
        }
        
        // Don't process if window is closing or not loaded
        if (!IsLoaded || IsClosing)
        {
            DebugLogger.Log("Window_Deactivated: IGNORING - window is closing or not loaded");
            return;
        }
        
        // Use a delayed auto-hide to avoid race conditions with hotkey toggles
        // Cancel any existing timer
        if (_deactivateTimer != null)
        {
            DebugLogger.Log("Window_Deactivated: Stopping existing deactivate timer");
            _deactivateTimer.Stop();
        }
        
        // Create new timer with 150ms delay
        DebugLogger.Log("Window_Deactivated: Starting 150ms delay timer before auto-hide");
        _deactivateTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _deactivateTimer.Tick += (s, args) =>
        {
            DebugLogger.LogHeader("Deactivate Timer Tick (after 150ms)");
            _deactivateTimer?.Stop();
            
            // Additional safety check - don't process if window is closing or not loaded
            if (!IsLoaded || _isClosing)
            {
                DebugLogger.Log("Deactivate Timer: IGNORING - window is closing or not loaded");
                return;
            }
            
            DebugLogger.LogVariable("Window.IsActive (now)", this.IsActive);
            DebugLogger.LogVariable("_isTogglingViaHotkey (now)", _isTogglingViaHotkey);
            DebugLogger.LogVariable("Window.Visibility (now)", this.Visibility);
            DebugLogger.LogVariable("_isClosing", _isClosing);
            
            // Check if widget launcher or timer overlay is active
            var isWidgetLauncherActive = _widgetLauncher != null && _widgetLauncher.IsActive;
            var isTimerOverlayActive = _timerOverlay != null && _timerOverlay.IsActive;
            
            DebugLogger.LogVariable("WidgetLauncher.IsActive", isWidgetLauncherActive);
            DebugLogger.LogVariable("TimerOverlay.IsActive", isTimerOverlayActive);
            
            // Double-check we're still deactivated and not toggling
            // Don't auto-hide if widget launcher or timer overlay is active
            if (!this.IsActive && !_isTogglingViaHotkey && this.Visibility == Visibility.Visible 
                && !isWidgetLauncherActive && !isTimerOverlayActive && !_isClosing)
            {
                DebugLogger.Log("Window_Deactivated: Conditions met -> AUTO-HIDING overlay");
                HideOverlay();
            }
            else
            {
                DebugLogger.Log($"Window_Deactivated: SKIPPING auto-hide:");
                DebugLogger.LogVariable("  Reason: IsActive", this.IsActive);
                DebugLogger.LogVariable("  Reason: _isTogglingViaHotkey", _isTogglingViaHotkey);
                DebugLogger.LogVariable("  Reason: Visibility", this.Visibility);
                DebugLogger.LogVariable("  Reason: _isClosing", _isClosing);
            }
        };
        _deactivateTimer.Start();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        DebugLogger.Log("Window_Closing: Setting closing state");
        _isClosing = true;
        
        // Save widget positions if in Living Widgets Mode
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        if (isLivingWidgetsMode)
        {
            _settings.SetSearchOverlayPosition(this.Left, this.Top);
            
            if (_widgetLauncher != null)
            {
                _settings.SetWidgetLauncherPosition(_widgetLauncher.Left, _widgetLauncher.Top);
            }
            
            // Save async but don't await (app is closing)
            _ = _settings.SaveAsync();
            DebugLogger.Log("Window_Closing: Saved widget positions");
        }
    }

    private void ShowLoading(bool show)
    {
        LoadingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private static System.Windows.Media.Color Blend(System.Windows.Media.Color baseColor, System.Windows.Media.Color tint, double amount)
    {
        amount = Math.Max(0, Math.Min(1, amount));
        byte r = (byte)Math.Round(baseColor.R + (tint.R - baseColor.R) * amount);
        byte g = (byte)Math.Round(baseColor.G + (tint.G - baseColor.G) * amount);
        byte b = (byte)Math.Round(baseColor.B + (tint.B - baseColor.B) * amount);
        return System.Windows.Media.Color.FromRgb(r, g, b);
    }

    private static double GetLuminance(System.Windows.Media.Color color)
    {
        return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
    }

    public void ReloadHotkey()
    {
        Dispatcher.Invoke(async () =>
        {
            try
            {
                _hotkey?.Dispose();
                
                await _settings.LoadAsync();
                var (modifiers, key) = _settings.GetHotkey();
                
                _hotkey = new GlobalHotkey(this, (uint)modifiers, (uint)key);
                _hotkey.HotkeyPressed += OnHotkeyPressed;
                
                StatusText.Text = $"Hotkey updated to {FormatHotkey(modifiers, key)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to update hotkey: {ex.Message}";
            }
        });
    }

    private void StartIpcListener()
    {
        _ipcCts = new CancellationTokenSource();
        var token = _ipcCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream("DesktopHub_IPC", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    DebugLogger.Log("IPC: Waiting for connection...");
                    
                    await server.WaitForConnectionAsync(token);
                    DebugLogger.Log("IPC: Client connected");

                    using var reader = new StreamReader(server);
                    var command = await reader.ReadLineAsync();
                    
                    if (!string.IsNullOrEmpty(command))
                    {
                        DebugLogger.Log($"IPC: Received command: {command}");
                        await Dispatcher.InvokeAsync(() => HandleIpcCommand(command));
                    }
                }
                catch (OperationCanceledException)
                {
                    DebugLogger.Log("IPC: Listener cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"IPC: Error: {ex.Message}");
                    await Task.Delay(1000, token);
                }
            }
        }, token);
    }

    private void HandleIpcCommand(string command)
    {
        DebugLogger.Log($"IPC: Handling command: {command}");
        
        switch (command)
        {
            case "SHOW_OVERLAY":
                if (this.Visibility != Visibility.Visible)
                {
                    ShowOverlay();
                }
                else
                {
                    this.Activate();
                    SearchBox.Focus();
                }
                break;

            case "SHOW_SETTINGS":
                _trayIcon?.ShowSettings();
                break;

            case "CLOSE_APP":
                var confirmed = ConfirmationDialog.Show("Are you sure you want to exit DesktopHub?", this);
                if (confirmed)
                {
                    System.Windows.Application.Current.Shutdown();
                }
                break;

            default:
                DebugLogger.Log($"IPC: Unknown command: {command}");
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _ipcCts?.Cancel();
        _ipcCts?.Dispose();
        _hotkey?.Dispose();
        _trayIcon?.Dispose();
        base.OnClosed(e);
    }

    // ViewModel for binding
    private void UpdateRootClip(double radiusDip)
    {
        try
        {
            DebugLogger.Log($"UpdateRootClip: Starting with radiusDip={radiusDip}");
            DebugLogger.Log($"  RootBorder.ActualWidth: {RootBorder.ActualWidth}");
            DebugLogger.Log($"  RootBorder.ActualHeight: {RootBorder.ActualHeight}");
            DebugLogger.Log($"  RootBorder.Width: {RootBorder.Width}");
            DebugLogger.Log($"  RootBorder.Height: {RootBorder.Height}");
            DebugLogger.Log($"  RootBorder.Margin: {RootBorder.Margin}");
            DebugLogger.Log($"  RootBorder.Padding: {RootBorder.Padding}");
            DebugLogger.Log($"  RootBorder.BorderThickness: {RootBorder.BorderThickness}");
            DebugLogger.Log($"  RootBorder.CornerRadius: {RootBorder.CornerRadius}");
            
            if (RootBorder.ActualWidth <= 0 || RootBorder.ActualHeight <= 0)
            {
                DebugLogger.Log("  Skipping clip - RootBorder not rendered yet");
                return;
            }
            
            var rect = new System.Windows.Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight);
            RootBorder.Clip = new System.Windows.Media.RectangleGeometry(rect, radiusDip, radiusDip);
            DebugLogger.Log($"  Applied clip geometry: {rect.Width}x{rect.Height} with radius {radiusDip}");
            DebugLogger.Log($"  RootBorder.Clip: {RootBorder.Clip}");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"UpdateRootClip: EXCEPTION - {ex}");
        }
    }

    // ViewModel for binding
    private class ProjectViewModel
    {
        public string FullNumber { get; }
        public string Name { get; }
        public string Path { get; }
        public string? Location { get; }
        public string? Status { get; }
        public bool IsFavorite { get; }

        public ProjectViewModel(Project project)
        {
            FullNumber = project.FullNumber;
            Name = project.Name;
            Path = project.Path;
            Location = project.Metadata?.Location;
            Status = project.Metadata?.Status;
            IsFavorite = project.Metadata?.IsFavorite ?? false;
        }
    }
    
    private void OnTimerWidgetRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_timerOverlay == null)
            {
                _timerOverlay = new TimerOverlay(_timerService, _settings);
                
                // Set Topmost based on Living Widgets Mode
                var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
                _timerOverlay.Topmost = !isLivingWidgetsMode; // False in Living Widgets Mode
                
                // Position timer overlay on same screen as search overlay
                PositionTimerOverlayOnSameScreen();
                
                _timerOverlay.Show();
                _timerOverlay.Tag = "WasVisible";
                DebugLogger.Log($"OnTimerWidgetRequested: Timer overlay created and shown (Topmost={_timerOverlay.Topmost})");
            }
            else
            {
                if (_timerOverlay.Visibility == Visibility.Visible)
                {
                    _timerOverlay.Visibility = Visibility.Hidden;
                    _timerOverlay.Tag = null;
                    DebugLogger.Log("OnTimerWidgetRequested: Timer overlay hidden");
                }
                else
                {
                    _timerOverlay.Visibility = Visibility.Visible;
                    _timerOverlay.Tag = "WasVisible";
                    DebugLogger.Log("OnTimerWidgetRequested: Timer overlay shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnTimerWidgetRequested: Error with timer overlay: {ex}");
            System.Windows.MessageBox.Show($"Error with timer overlay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void PositionTimerOverlayOnSameScreen()
    {
        if (_timerOverlay == null)
            return;
            
        try
        {
            // Get the screen containing the search overlay
            var searchOverlayCenter = new System.Drawing.Point(
                (int)(this.Left + this.Width / 2),
                (int)(this.Top + this.Height / 2)
            );
            var screen = Screen.FromPoint(searchOverlayCenter);
            var workArea = screen.WorkingArea;
            
            // Position timer in bottom-right corner of the same screen
            _timerOverlay.Left = workArea.Right - _timerOverlay.Width - 20;
            _timerOverlay.Top = workArea.Bottom - _timerOverlay.Height - 20;
            
            DebugLogger.Log($"PositionTimerOverlayOnSameScreen: Timer positioned at ({_timerOverlay.Left}, {_timerOverlay.Top}) on screen {screen.DeviceName}");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"PositionTimerOverlayOnSameScreen: Error positioning timer: {ex.Message}");
        }
    }
    
    private void EnableWindowDragging()
    {
        // Remove handlers first to prevent duplicates when switching modes
        this.MouseLeftButtonDown -= Window_MouseLeftButtonDown;
        this.MouseLeftButtonUp -= Window_MouseLeftButtonUp;
        this.MouseMove -= Window_MouseMove;
        
        // Add mouse event handlers for dragging when Living Widgets Mode is enabled
        this.MouseLeftButtonDown += Window_MouseLeftButtonDown;
        this.MouseLeftButtonUp += Window_MouseLeftButtonUp;
        this.MouseMove += Window_MouseMove;
    }

    private void DisableWindowDragging()
    {
        // Remove mouse event handlers when Living Widgets Mode is disabled
        this.MouseLeftButtonDown -= Window_MouseLeftButtonDown;
        this.MouseLeftButtonUp -= Window_MouseLeftButtonUp;
        this.MouseMove -= Window_MouseMove;
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Only allow dragging if Living Widgets Mode is enabled and not clicking on interactive elements
        if (!_settings.GetLivingWidgetsMode())
            return;
            
        // Don't start drag if clicking on interactive elements (textbox, buttons, list)
        var element = e.OriginalSource as FrameworkElement;
        if (element != null)
        {
            // Allow dragging only from non-interactive areas (borders, panels, window background)
            var clickedType = element.GetType().Name;
            if (clickedType == "TextBox" || clickedType == "Button" || clickedType == "ListBoxItem" || 
                clickedType == "ComboBox" || clickedType == "ScrollBar" || clickedType == "Thumb")
            {
                return;
            }
        }
        
        _isDragging = true;
        _dragStartPoint = e.GetPosition(this);
        this.CaptureMouse();
        DebugLogger.Log("Window dragging started");
    }

    private void Window_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();
            
            // Apply snap to screen edges if close
            SnapToScreenEdges();
            DebugLogger.Log("Window dragging ended");
        }
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var currentPosition = e.GetPosition(this);
            var offset = currentPosition - _dragStartPoint;
            
            this.Left += offset.X;
            this.Top += offset.Y;
            
            // Only update widget launcher position in Legacy mode (keep attached)
            // In Living Widgets Mode, widgets are independent
            var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
            if (!isLivingWidgetsMode)
            {
                UpdateWidgetLauncherPosition();
            }
        }
    }

    private void SnapToScreenEdges()
    {
        try
        {
            // Get the screen containing the window
            var windowCenter = new System.Drawing.Point(
                (int)(this.Left + this.Width / 2),
                (int)(this.Top + this.Height / 2)
            );
            var screen = Screen.FromPoint(windowCenter);
            var workArea = screen.WorkingArea;
            
            const int snapThreshold = 20; // pixels from edge to trigger snap
            
            // Snap to left edge
            if (Math.Abs(this.Left - workArea.Left) < snapThreshold)
            {
                this.Left = workArea.Left + 10; // 10px margin
                DebugLogger.Log("Snapped to left edge");
            }
            
            // Snap to right edge
            if (Math.Abs(this.Left + this.Width - workArea.Right) < snapThreshold)
            {
                this.Left = workArea.Right - this.Width - 10;
                DebugLogger.Log("Snapped to right edge");
            }
            
            // Snap to top edge
            if (Math.Abs(this.Top - workArea.Top) < snapThreshold)
            {
                this.Top = workArea.Top + 10;
                DebugLogger.Log("Snapped to top edge");
            }
            
            // Snap to bottom edge
            if (Math.Abs(this.Top + this.Height - workArea.Bottom) < snapThreshold)
            {
                this.Top = workArea.Bottom - this.Height - 10;
                DebugLogger.Log("Snapped to bottom edge");
            }
            
            // Snap to top-left corner
            if (Math.Abs(this.Left - workArea.Left) < snapThreshold && 
                Math.Abs(this.Top - workArea.Top) < snapThreshold)
            {
                this.Left = workArea.Left + 10;
                this.Top = workArea.Top + 10;
                DebugLogger.Log("Snapped to top-left corner");
            }
            
            // Snap to top-right corner
            if (Math.Abs(this.Left + this.Width - workArea.Right) < snapThreshold && 
                Math.Abs(this.Top - workArea.Top) < snapThreshold)
            {
                this.Left = workArea.Right - this.Width - 10;
                this.Top = workArea.Top + 10;
                DebugLogger.Log("Snapped to top-right corner");
            }
            
            // Snap to bottom-left corner
            if (Math.Abs(this.Left - workArea.Left) < snapThreshold && 
                Math.Abs(this.Top + this.Height - workArea.Bottom) < snapThreshold)
            {
                this.Left = workArea.Left + 10;
                this.Top = workArea.Bottom - this.Height - 10;
                DebugLogger.Log("Snapped to bottom-left corner");
            }
            
            // Snap to bottom-right corner
            if (Math.Abs(this.Left + this.Width - workArea.Right) < snapThreshold && 
                Math.Abs(this.Top + this.Height - workArea.Bottom) < snapThreshold)
            {
                this.Left = workArea.Right - this.Width - 10;
                this.Top = workArea.Bottom - this.Height - 10;
                DebugLogger.Log("Snapped to bottom-right corner");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SnapToScreenEdges: Error: {ex.Message}");
        }
    }

    private void UpdateWidgetLauncherPosition()
    {
        if (_widgetLauncher != null && _widgetLauncher.Visibility == Visibility.Visible)
        {
            var windowWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
            _widgetLauncher.Left = this.Left + windowWidth + 12;
            _widgetLauncher.Top = this.Top;
        }
    }
    
    public void UpdateDraggingMode()
    {
        // Called when Living Widgets Mode setting changes
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        
        if (isLivingWidgetsMode)
        {
            EnableWindowDragging();
            this.Topmost = false; // Live on desktop, not always on top
            
            // Enable dragging and disable Topmost for widget launcher too
            if (_widgetLauncher != null)
            {
                _widgetLauncher.EnableDragging();
                _widgetLauncher.Topmost = false;
            }
            
            // Disable Topmost for timer overlay too if it exists
            if (_timerOverlay != null)
            {
                _timerOverlay.Topmost = false;
            }
            
            DebugLogger.Log("Window dragging enabled (Living Widgets Mode ON) - Topmost disabled");
        }
        else
        {
            DisableWindowDragging();
            this.Topmost = true; // Legacy mode: always on top
            
            // Disable dragging and enable Topmost for widget launcher too
            if (_widgetLauncher != null)
            {
                _widgetLauncher.DisableDragging();
                _widgetLauncher.Topmost = true;
            }
            
            // Enable Topmost for timer overlay too if it exists
            if (_timerOverlay != null)
            {
                _timerOverlay.Topmost = true;
            }
            
            DebugLogger.Log("Window dragging disabled (Living Widgets Mode OFF) - Topmost enabled");
        }
    }

    public async void OnDriveSettingsChanged()
    {
        try
        {
            DebugLogger.Log("OnDriveSettingsChanged: Drive settings changed, reloading projects...");
            
            // Reload projects from database to apply new filtering
            await Dispatcher.InvokeAsync(async () => 
            {
                await LoadProjectsAsync();
                
                // Trigger a background scan to pick up newly enabled drives
                _ = BackgroundScanAsync();
            });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnDriveSettingsChanged: Error reloading projects: {ex.Message}");
        }
    }

    public void UpdateTransparency()
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                // Update search overlay transparency
                var overlayTransparency = _settings.GetOverlayTransparency();
                var overlayAlpha = (byte)(overlayTransparency * 255);
                RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(overlayAlpha, 0x18, 0x18, 0x18));
                
                DebugLogger.Log($"UpdateTransparency: SearchOverlay transparency updated to {overlayTransparency:F2}");
                
                // Update widget launcher transparency if it exists
                if (_widgetLauncher != null)
                {
                    _widgetLauncher.UpdateTransparency();
                }
                
                // Update timer overlay transparency if it exists
                if (_timerOverlay != null)
                {
                    _timerOverlay.UpdateTransparency();
                }
            });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"UpdateTransparency: Error updating transparency: {ex.Message}");
        }
    }
}
