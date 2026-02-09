# Known Issues

## Virtual Desktop Following (Windows 11)

**Status:** FIXED - Using desktop switch detection + window migration approach

**Issue:** In Living Widgets Mode, widgets do not follow the active virtual desktop. When switching desktops, the widgets remain on the original desktop instead of appearing on all desktops.

**Root Cause:** The Windows `IVirtualDesktopManager` COM interface (CLSID `AA509086-5CA9-4C25-8F95-589D3C07B48A`) causes application crashes when calling `PinWindow()` or `UnpinWindow()` methods, even with extensive safety measures:
- Window validation with Win32 `IsWindow()` API
- Thread-safe locking mechanisms
- Redundancy checks (IsWindowPinned before pinning)
- Async operations with delays for window initialization
- Background thread isolation
- Comprehensive error handling with COM exception catching

**Attempted Solutions:**
1. ✗ Basic COM interop (crashed)
2. ✗ Extensive null checking and error handling (crashed)
3. ✗ Window readiness validation with delays (crashed)
4. ✗ Thread-safe operations with locks (crashed)
5. ✗ Async background thread execution (crashed)

**Crash Pattern:**
```
VirtualDesktopManager: Successfully initialized
[immediate crash - no exception caught]
```

**Implementation Details:**
See `c:\Users\smarkowitz\repos\desktop-widgets\DesktopHub\src\DesktopHub.UI\Helpers\VirtualDesktopManager.cs` for the comprehensive safety implementation that still failed.

**Current Workaround:** 
Widgets remain on their original virtual desktop. Users must manually use the hotkey to bring widgets to focus, which switches to the desktop where widgets live.

**Potential Future Solutions:**
1. **Virtual Desktop Event Monitoring:** Monitor desktop switch events and automatically move/reposition windows (complex)
2. **Third-party Libraries:** Use community virtual desktop libraries (may have same issues)
3. **Wait for Windows API Updates:** Microsoft may fix COM interface stability in future Windows updates
4. **Accept as Known Limitation:** Document behavior and focus on other features

**Related Code:**
- `SearchOverlay.xaml.cs:269-277` - Window_Loaded pinning (disabled)
- `SearchOverlay.xaml.cs:2163-2164` - UpdateDraggingMode pinning (disabled)
- `SearchOverlay.xaml.cs:2186-2187` - UpdateDraggingMode unpinning (disabled)
- `SearchOverlay.xaml.cs:1811-1825` - Timer overlay pinning (disabled)
- `VirtualDesktopManager.cs` - Full implementation with safety features

**Last Updated:** February 5, 2026
