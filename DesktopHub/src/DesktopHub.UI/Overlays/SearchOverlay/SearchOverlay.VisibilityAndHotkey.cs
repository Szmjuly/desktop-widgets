using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    internal void RegisterHotkeysFromGroups()
    {
        // Dispose all existing hotkeys
        foreach (var hk in _hotkeys)
            hk.Dispose();
        _hotkeys.Clear();

        var groups = _settings.GetHotkeyGroups();
        foreach (var group in groups)
        {
            if (group.Key == 0) continue; // Skip unassigned groups
            var capturedWidgets = group.Widgets.ToList();
            var capturedMods = group.Modifiers;
            var capturedKey = group.Key;
            try
            {
                var hk = new GlobalHotkey(this, (uint)capturedMods, (uint)capturedKey);
                hk.HotkeyPressed += (s, e) => OnHotkeyPressed(capturedWidgets, capturedMods, capturedKey);
                hk.ShouldSuppressHotkey = () => ShouldSuppressHotkey(capturedMods, capturedKey);
                _hotkeys.Add(hk);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to register hotkey ({FormatHotkey(capturedMods, capturedKey)}). {ex.Message}",
                    "DesktopHub", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
    }

    private void OnHotkeyPressed(IReadOnlyList<string> targetWidgets, int modifiers, int key)
    {
        DebugLogger.LogSeparator("HOTKEY PRESSED");

        // Debounce rapid hotkey presses
        var now = DateTime.Now;
        if ((now - _lastHotkeyPress).TotalMilliseconds < 200)
            return;
        _lastHotkeyPress = now;

        Dispatcher.Invoke(() =>
        {
            _isTogglingViaHotkey = true;

            if (_deactivateTimer != null)
                _deactivateTimer.Stop();

            var triggersSearch = targetWidgets.Contains(WidgetIds.SearchOverlay);
            if (triggersSearch)
            {
                ShowOverlay(targetWidgets);
            }
            else
            {
                // No search overlay in this group — just focus the listed widgets
                FocusWidgetsForGroup(targetWidgets);
            }

            Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() => _isTogglingViaHotkey = false));
        });
    }

    private void FocusWidgetsForGroup(IReadOnlyList<string> targetWidgets)
    {
        if (targetWidgets.Contains(WidgetIds.WidgetLauncher) && _widgetLauncher != null)
        {
            _widgetLauncher.Visibility = Visibility.Visible;
            BringWidgetToForegroundIfEnabled(_widgetLauncher, true);
        }
        BringWidgetToForegroundIfEnabled(_timerOverlay, targetWidgets.Contains(WidgetIds.Timer));
        BringWidgetToForegroundIfEnabled(_quickTasksOverlay, targetWidgets.Contains(WidgetIds.QuickTasks));
        BringWidgetToForegroundIfEnabled(_docOverlay, targetWidgets.Contains(WidgetIds.DocQuickOpen));
        BringWidgetToForegroundIfEnabled(_frequentProjectsOverlay, targetWidgets.Contains(WidgetIds.FrequentProjects));
        BringWidgetToForegroundIfEnabled(_quickLaunchOverlay, targetWidgets.Contains(WidgetIds.QuickLaunch));
        BringWidgetToForegroundIfEnabled(_smartProjectSearchOverlay, targetWidgets.Contains(WidgetIds.SmartProjectSearch));
        BringWidgetToForegroundIfEnabled(_cheatSheetOverlay, targetWidgets.Contains(WidgetIds.CheatSheet));
    }

    private bool ShouldSuppressHotkey(int modifiers, int key)
    {
        DebugLogger.LogHeader("ShouldSuppressHotkey Called");
        DebugLogger.LogVariable("Modifiers", modifiers);
        DebugLogger.LogVariable("Key", key);
        DebugLogger.LogVariable("Hotkey", FormatHotkey(modifiers, key));
        DebugLogger.LogVariable("Window.Visibility", this.Visibility);

        // Feature requirement: Ctrl+Alt+Space should always bring up search.
        // Never suppress the hotkey.
        DebugLogger.Log("ShouldSuppressHotkey: NOT suppressing - hotkey should always trigger");
        return false;
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

    private void ShowOverlay(IReadOnlyList<string>? targetWidgets = null)
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

        // In Living Widgets Mode, if the overlay is already visible, just re-focus it
        // without clearing search state or reloading projects (prevents results flash)
        var isAlreadyVisible = this.Visibility == Visibility.Visible && this.Opacity > 0;
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        var hasSearchContent = !string.IsNullOrEmpty(SearchBox.Text) || ResultsList.Items.Count > 0;
        if (isAlreadyVisible && isLivingWidgetsMode && hasSearchContent)
        {
            DebugLogger.Log("ShowOverlay: Already visible in Living Widgets Mode — just re-focusing");

            // Bring widgets to foreground based on group membership
            var tw = targetWidgets;
            if (_widgetLauncher != null)
            {
                _widgetLauncher.Visibility = Visibility.Visible;
                BringWidgetToForegroundIfEnabled(_widgetLauncher, tw == null || tw.Contains(WidgetIds.WidgetLauncher));
            }
            BringWidgetToForegroundIfEnabled(_timerOverlay, tw == null || tw.Contains(WidgetIds.Timer));
            BringWidgetToForegroundIfEnabled(_quickTasksOverlay, tw == null || tw.Contains(WidgetIds.QuickTasks));
            BringWidgetToForegroundIfEnabled(_docOverlay, tw == null || tw.Contains(WidgetIds.DocQuickOpen));
            BringWidgetToForegroundIfEnabled(_frequentProjectsOverlay, tw == null || tw.Contains(WidgetIds.FrequentProjects));
            BringWidgetToForegroundIfEnabled(_quickLaunchOverlay, tw == null || tw.Contains(WidgetIds.QuickLaunch));
            BringWidgetToForegroundIfEnabled(_smartProjectSearchOverlay, tw == null || tw.Contains(WidgetIds.SmartProjectSearch));
            BringWidgetToForegroundIfEnabled(_cheatSheetOverlay, tw == null || tw.Contains(WidgetIds.CheatSheet));

            this.Activate();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchBox.Focus();
            }), System.Windows.Threading.DispatcherPriority.Input);
            return;
        }

        ApplyDynamicTinting();

        // Reset manual toggle flag on new open
        _userManuallySizedResults = false;

        ApplySmartProjectSearchAttachModeState();

        // Retain smart search panel state if it was already expanded;
        // only collapse the main results + panel on a fresh open (panel not yet expanded)
        var retainSmartSearchState = _isSmartProjectSearchAttachedPanelExpanded
                                     && _smartProjectSearchAttachedWidget != null;

        if (retainSmartSearchState)
        {
            // Keep results visible so the smart search panel stays in context
            _isResultsCollapsed = false;
            ResultsContainer.Visibility = Visibility.Visible;
            CollapseIconRotation.Angle = 0;
            // Re-apply current expanded height without animation
            SetSmartProjectSearchAttachedPanelExpanded(true, false);
        }
        else
        {
            // Start with results collapsed
            _isResultsCollapsed = true;
            ResultsContainer.Visibility = Visibility.Collapsed;
            CollapseIconRotation.Angle = -90;
            SetSmartProjectSearchAttachedPanelExpanded(false, false);
        }
        UpdateOverlayHeightForCurrentState(false);

        DebugLogger.LogHeader("Positioning Window");
        // Only reposition if Living Widgets Mode is disabled (legacy overlay mode)
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

        // Reposition smart search floating window below the overlay
        if (retainSmartSearchState)
            PositionSmartSearchAttachedWindow();

        // Show widget launcher next to search overlay
        if (_widgetLauncher != null)
        {
            // Only auto-attach in Legacy mode; in Living Widgets Mode, remember position
            if (!isLivingWidgetsMode)
            {
                var windowWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
                _widgetLauncher.Left = this.Left + windowWidth + GetConfiguredWidgetGap();
                _widgetLauncher.Top = this.Top;
            }

            _widgetLauncher.Visibility = Visibility.Visible;
        }

        // Bring widgets to foreground (without stealing focus from the search overlay)
        // using Topmost toggle trick — Activate() would steal keyboard focus and grey out the overlay
        BringWidgetToForegroundIfEnabled(_widgetLauncher, targetWidgets == null || targetWidgets.Contains(WidgetIds.WidgetLauncher));
        BringWidgetToForegroundIfEnabled(_timerOverlay, targetWidgets == null || targetWidgets.Contains(WidgetIds.Timer));
        BringWidgetToForegroundIfEnabled(_quickTasksOverlay, targetWidgets == null || targetWidgets.Contains(WidgetIds.QuickTasks));
        BringWidgetToForegroundIfEnabled(_docOverlay, targetWidgets == null || targetWidgets.Contains(WidgetIds.DocQuickOpen));
        BringWidgetToForegroundIfEnabled(_frequentProjectsOverlay, targetWidgets == null || targetWidgets.Contains(WidgetIds.FrequentProjects));
        BringWidgetToForegroundIfEnabled(_quickLaunchOverlay, targetWidgets == null || targetWidgets.Contains(WidgetIds.QuickLaunch));
        BringWidgetToForegroundIfEnabled(_smartProjectSearchOverlay, targetWidgets == null || targetWidgets.Contains(WidgetIds.SmartProjectSearch));
        BringWidgetToForegroundIfEnabled(_cheatSheetOverlay, targetWidgets == null || targetWidgets.Contains(WidgetIds.CheatSheet));

        DebugLogger.LogHeader("Calling Window.Activate()");
        var activateResult = this.Activate();
        DebugLogger.LogVariable("Activate() returned", activateResult);
        DebugLogger.LogVariable("Window.IsActive after Activate()", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused after Activate()", this.IsFocused);

        if (retainSmartSearchState)
        {
            // Retain existing search text and results — just re-focus the search box
            DebugLogger.Log("ShowOverlay: Retaining search state (smart search was expanded)");
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
        else
        {
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
            }), System.Windows.Threading.DispatcherPriority.Input);

            // Load all projects filtered by year
            LoadAllProjects();

            // Show history if search is blank
            UpdateHistoryVisibility();
        }
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
            RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x12, 0x12, 0x12));
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
            SetSmartProjectSearchAttachedPanelExpanded(false, true);
            UpdateOverlayHeightForCurrentState(true);
        }
        else
        {
            // Expand results - restore full window height
            ResultsContainer.Visibility = Visibility.Visible;
            CollapseIconRotation.Angle = 0; // Arrow points down
            UpdateOverlayHeightForCurrentState(true);
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

        // In Living Widgets Mode, widget launcher is independent - don't auto-hide
        // Only hide widget launcher in legacy mode (when not in Living Widgets Mode)
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        if (!isLivingWidgetsMode && _widgetLauncher != null)
        {
            _widgetLauncher.Visibility = Visibility.Hidden;
            DebugLogger.Log("HideOverlay: Also hid widget launcher (legacy mode)");
        }
        else if (isLivingWidgetsMode)
        {
            DebugLogger.Log("HideOverlay: Widget launcher remains independent (Living Widgets Mode)");
        }

        // Timer overlay is now independent - don't auto-hide with search overlay

        // Hide the smart search attached window (but retain its expanded state)
        if (_smartProjectSearchAttachedWindow != null)
            _smartProjectSearchAttachedWindow.Visibility = Visibility.Hidden;

        // When smart search panel is expanded, preserve search state for re-open
        if (!_isSmartProjectSearchAttachedPanelExpanded)
        {
            DebugLogger.LogHeader("Clearing SearchBox and UI");
            SearchBox.Clear();
            ResultsList.ItemsSource = null;

            // Clear history pills to prevent them from reappearing
            HorizontalHistoryList.ItemsSource = null;
            HistoryAndCollapseContainer.Visibility = Visibility.Collapsed;
        }
        else
        {
            DebugLogger.Log("HideOverlay: Preserving search state (smart search panel expanded)");
        }

        DebugLogger.LogHeader("Final State");
        DebugLogger.LogVariable("Window.Visibility", this.Visibility);
        DebugLogger.LogVariable("Window.IsVisible", this.IsVisible);
        DebugLogger.LogVariable("SearchBox.IsFocused (final)", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused (final)", SearchBox.IsKeyboardFocused);
        DebugLogger.Log("HideOverlay: Returning from method");
    }

    private static void BringWidgetToForegroundIfEnabled(Window? widget, bool enabled)
    {
        if (!enabled || widget == null || widget.Visibility != Visibility.Visible)
            return;

        // Bring to foreground without stealing keyboard focus:
        // temporarily set Topmost to force the window above others, then restore.
        var wasTopmost = widget.Topmost;
        widget.Topmost = true;
        widget.Topmost = wasTopmost;
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

        var keyLabel = KeyInterop.KeyFromVirtualKey(key);
        var keyText = keyLabel != Key.None ? keyLabel.ToString() : $"0x{key:X}";
        parts.Add(keyText);

        return string.Join("+", parts);
    }
}
