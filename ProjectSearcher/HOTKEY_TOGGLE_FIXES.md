# Hotkey Toggle Fixes - Summary

## Issues Fixed

### 1. Cannot Repeatedly Toggle Overlay
**Root Cause**: Race condition between `Window_Deactivated` and hotkey toggle handler. When pressing the hotkey to close the overlay, `Window_Deactivated` would fire immediately and interfere with the toggle operation.

**Solution**: 
- Added `_isTogglingViaHotkey` flag to track when a hotkey toggle is in progress
- Added 150ms delayed timer in `Window_Deactivated` to avoid race conditions
- Hotkey toggle cancels any pending deactivation timers

### 2. Difficulty Closing Overlay with Shortcut
**Root Cause**: Same race condition - `Window_Deactivated` would fire before the hotkey handler could properly hide the window.

**Solution**: 
- Track toggle state and prevent auto-hide during hotkey operations
- Added 200ms debouncing to prevent rapid double-triggering
- Reset toggle flag after 300ms to allow normal auto-hide behavior

### 3. Focus Issues and Context-Aware Shortcut Handling
**Root Cause**: Overly aggressive suppression logic that blocked the hotkey from working in ANY app when using shift+character combinations.

**Solution**:
- Refined `ShouldSuppressHotkeyForTyping` to only check text fields when **opening** the overlay
- When **closing** the overlay, always allow the hotkey (user intentionally pressed it)
- Removed fallback logic that blocked shift+character hotkeys when other apps had focus
- Now properly detects text input fields using UIAutomation (Edit, Document, ComboBox controls)

## Technical Changes

### New State Tracking Fields
```csharp
private bool _isTogglingViaHotkey = false;
private DateTime _lastHotkeyPress = DateTime.MinValue;
private System.Windows.Threading.DispatcherTimer? _deactivateTimer;
```

### Updated Methods

#### `OnHotkeyPressed`
- Added 200ms debouncing
- Sets `_isTogglingViaHotkey` flag during toggle operations
- Cancels pending deactivate timers
- Passes overlay visibility state to suppression check

#### `ShouldSuppressHotkeyForTyping`
- Now accepts `isCurrentlyVisible` parameter
- Only checks for text fields when opening overlay (not closing)
- More accurate text field detection using UIAutomation patterns
- Removed overly aggressive foreground process check

#### `Window_Deactivated`
- Added 150ms delayed timer to prevent race conditions
- Checks `_isTogglingViaHotkey` flag before auto-hiding
- Verifies window is still inactive before hiding

### Removed Code
- `IsShiftCharacterHotkey()` helper function (unused)
- `IsCharacterKey()` helper function (unused)
- `GetForegroundWindow()` Win32 import (unused)
- `GetWindowThreadProcessId()` Win32 import (unused)

## Expected Behavior

### Opening Overlay
✅ Works from desktop with any hotkey
✅ Works from Chrome, Firefox, Windsurf with any hotkey
✅ **Suppressed** only when focus is in a text input field (Edit/Document controls)
✅ Works repeatedly without issues

### Closing Overlay
✅ Always works regardless of which app has focus
✅ Always works even with shift+character hotkeys
✅ No race conditions or delayed closes

### Context Awareness
✅ Detects text inputs in: REVU, Chrome, Firefox, Windsurf, etc.
✅ Allows overlay in non-text contexts (toolbars, menus, etc.)
✅ Proper focus management across applications

## Testing Checklist

- [ ] Open overlay from desktop → works
- [ ] Close overlay from desktop → works
- [ ] Open overlay from Chrome address bar → suppressed (typing)
- [ ] Open overlay from Chrome toolbar/page → works
- [ ] Close overlay while in Chrome → works
- [ ] Open overlay from text editor → suppressed
- [ ] Open overlay from Windsurf sidebar → works
- [ ] Rapidly toggle overlay 5+ times → no issues
- [ ] Open from REVU text annotation → suppressed
- [ ] Open from REVU toolbar → works

## Notes

- Debounce timers prevent rapid double-triggering
- Auto-hide still works when clicking away (with 150ms delay)
- Logging added for debugging toggle operations
- All changes maintain backward compatibility
