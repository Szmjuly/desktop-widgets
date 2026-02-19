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
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
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

            // Hotkey behavior: always bring up search, clear query, and focus typing.
            // ShowOverlay() already handles clear + focus and safely works when already visible.
            DebugLogger.Log("OnHotkeyPressed: FORCING overlay open + focus");
            ShowOverlay();

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
                _widgetLauncher.Left = this.Left + windowWidth + GetConfiguredWidgetGap();
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

        var keyLabel = KeyInterop.KeyFromVirtualKey(key);
        var keyText = keyLabel != Key.None ? keyLabel.ToString() : $"0x{key:X}";
        parts.Add(keyText);

        return string.Join("+", parts);
    }
}
