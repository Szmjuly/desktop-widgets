# Shift+Character Hotkey Fixes - Summary

## Issues Fixed

### 1. ✅ Hotkey Not Suppressed When Typing in Other Apps (Windsurf, etc.)
**Problem**: When focused in Windsurf or other application text boxes, pressing Shift+A would open the overlay instead of typing "A".

**Solution**: 
- Enhanced UIAutomation detection to properly identify Edit, Document, and ComboBox controls
- Added logging to track when suppression occurs
- Only suppress when **opening** overlay (not closing) to allow user to close from anywhere

**Files Changed**:
- `SearchOverlay.xaml.cs:239-315` - Added `ShouldSuppressHotkey()` and enhanced `ShouldSuppressHotkeyForTyping()`

### 2. ✅ Character Passthrough When Spamming Hotkey
**Problem**: Even when suppressed, rapidly pressing Shift+A would cause "A" characters to appear in text boxes.

**Solution**:
- Modified keyboard hook to **block** (return `1`) keyboard events when hotkey matches
- Blocks both WM_KEYDOWN and WM_KEYUP events to prevent any passthrough
- Added `_suppressingHotkeyEvents` flag to track when events should be blocked

**Files Changed**:
- `GlobalHotkey.cs:30` - Added `_suppressingHotkeyEvents` flag
- `GlobalHotkey.cs:33` - Added `ShouldSuppressHotkey` callback
- `GlobalHotkey.cs:93-133` - Modified `HookCallback()` to block keyboard events when hotkey matches

### 3. ✅ Cannot Type in Overlay's SearchBox with Shift+Character Hotkey
**Problem**: When Shift+A is the hotkey and overlay is open with SearchBox focused, user cannot type "A" characters because hotkey keeps triggering.

**Solution**:
- Check if overlay's own `SearchBox.IsFocused` before processing hotkey
- ALWAYS suppress hotkey when typing in overlay's SearchBox
- Allows all other modifiers (Ctrl+Alt+Space, etc.) to still close overlay

**Files Changed**:
- `SearchOverlay.xaml.cs:239-249` - Added `ShouldSuppressHotkey()` with SearchBox focus check
- `SearchOverlay.xaml.cs:162` - Registered callback with GlobalHotkey

### 4. ✅ Initial Character Appears in SearchBox When Opening
**Problem**: When opening overlay with Shift+A, the SearchBox would start with "A" already typed in it.

**Solution**:
- Clear SearchBox immediately before showing overlay
- Delay focus using `Dispatcher.BeginInvoke` with `DispatcherPriority.Input` to ensure keyboard events are processed first
- Added safety check to clear any unexpected text that appears after 50ms

**Files Changed**:
- `SearchOverlay.xaml.cs:336-355` - Modified `ShowOverlay()` to clear and delay focus

## Technical Implementation

### GlobalHotkey.cs Changes

**New Callback System**:
```csharp
public Func<bool>? ShouldSuppressHotkey;
```

**Event Blocking**:
```csharp
if (!shouldSuppress)
{
    _hotkeyPressed = true;
    _suppressingHotkeyEvents = true;
    HotkeyPressed?.Invoke(this, EventArgs.Empty);
    
    // Block the keyboard event
    return (IntPtr)1;  // ← Prevents character passthrough
}
```

### SearchOverlay.xaml.cs Changes

**Multi-Layer Suppression**:
1. **Layer 1**: Check if overlay's SearchBox is focused → Always suppress
2. **Layer 2**: Check if opening overlay and other app has text field focused → Suppress
3. **Layer 3**: When closing overlay → Never suppress (user intended action)

**Focus Handling**:
```csharp
SearchBox.Clear();  // Clear first
Dispatcher.BeginInvoke(() => {
    SearchBox.Focus();  // Focus after input events processed
    SearchBox.SelectAll();
    
    // Safety check after 50ms
    Task.Delay(50).ContinueWith(_ => ClearUnexpectedText());
}, DispatcherPriority.Input);
```

## Testing Scenarios

### ✅ Test Case 1: Windsurf Text Box
- **Action**: Focus in Windsurf text editor, press Shift+A
- **Expected**: "A" appears in Windsurf, overlay does NOT open
- **Result**: Fixed - UIAutomation detects text field

### ✅ Test Case 2: Windsurf Sidebar (Non-Text)
- **Action**: Click Windsurf sidebar (not a text field), press Shift+A
- **Expected**: Overlay opens
- **Result**: Fixed - Only suppresses for text controls

### ✅ Test Case 3: Spamming Hotkey
- **Action**: Rapidly press Shift+A 10 times in a text box
- **Expected**: No characters appear in text box, overlay may open once
- **Result**: Fixed - Keyboard events blocked when hotkey matches

### ✅ Test Case 4: Typing in Overlay SearchBox
- **Action**: Open overlay, focus in SearchBox, press Shift+A
- **Expected**: "A" appears in SearchBox, overlay stays open
- **Result**: Fixed - SearchBox.IsFocused check suppresses hotkey

### ✅ Test Case 5: Closing from Overlay SearchBox
- **Action**: Open overlay, focus in SearchBox, press Ctrl+Alt+Space
- **Expected**: Overlay closes
- **Result**: Fixed - Non-character modifiers still work

### ✅ Test Case 6: No Initial Character
- **Action**: Press Shift+A to open overlay
- **Expected**: SearchBox is empty and ready for input
- **Result**: Fixed - Delayed focus + clear prevents initial "A"

## Debugging

All suppression decisions are logged to `Desktop/ProjectSearcher_Debug.log`:
- `ShouldSuppressHotkey: Suppressing - overlay's SearchBox is focused`
- `ShouldSuppressHotkeyForTyping: Suppressing - text control detected: Edit`
- `ShouldSuppressHotkeyForTyping: UIAutomation error: ...`
- `ShowOverlay: Clearing unexpected text in SearchBox: 'A'`

## Architecture

```
User presses Shift+A
    ↓
GlobalHotkey.HookCallback()
    ↓
Calls ShouldSuppressHotkey()
    ↓
    ├─ YES → Allow keyboard event to pass through (user typing)
    │        return CallNextHookEx(...)
    │
    └─ NO → Block keyboard event (trigger hotkey)
            return (IntPtr)1
            ↓
            Invoke HotkeyPressed event
            ↓
            SearchOverlay.OnHotkeyPressed()
            ↓
            ShowOverlay() / HideOverlay()
```

## Critical Fix: Cascade/Windsurf Text Input (2026-01-20)

### Problem
When typing in Cascade/Windsurf chat input, UIAutomation would fail with `RPC_E_CANTCALLOUT_ININPUTSYNCCALL`:
```
ShouldSuppressHotkeyForTyping: UIAutomation error: An outgoing call cannot be made since the application is dispatching an input-synchronous call. (0x8001010D)
OnHotkeyPressed: Hotkey triggered  ← WRONG! Should have been suppressed
OnHotkeyPressed: Showing overlay
```

This caused the overlay to open while typing "A" in Cascade chat.

### Solution
Changed default behavior when UIAutomation fails:
- **Before**: Default to allowing hotkey (show overlay)
- **After**: Default to suppressing hotkey (assume user is typing)

**Code Change** (`SearchOverlay.xaml.cs:306-314`):
```csharp
catch (Exception ex)
{
    DebugLogger.Log($"ShouldSuppressHotkeyForTyping: UIAutomation error: {ex.Message}");
    // Default to SUPPRESSING to be safe - assume user is typing
    DebugLogger.Log("ShouldSuppressHotkeyForTyping: Defaulting to SUPPRESS (safe assumption)");
    return true;  // ← Changed from allowing to suppressing
}
```

### Impact
✅ Overlay no longer opens when typing in Cascade/Windsurf chat  
✅ Works for any app where UIAutomation cannot detect focus  
⚠️ May require non-text click to open overlay in some apps (safer default)

## Notes

- Debouncing (200ms) prevents rapid double-triggers
- Toggle state tracking prevents race conditions
- **UIAutomation failure → defaults to SUPPRESSING (safe assumption user is typing)**
- Shift+character hotkeys are now fully context-aware
- All other modifier combinations (Ctrl+Alt, etc.) work everywhere
