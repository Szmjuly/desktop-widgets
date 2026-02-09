using System;
using System.Runtime.InteropServices;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Provides access to the PUBLIC Windows IVirtualDesktopManager COM interface.
/// IMPORTANT: This interface only has 3 methods. Previous crashes were caused by
/// defining extra methods (PinWindow/UnpinWindow/IsWindowPinned) that don't exist
/// on this COM object - calling them read garbage from the vtable.
/// </summary>
public static class VirtualDesktopManager
{
    private static readonly Guid CLSID_VirtualDesktopManager = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");
    
    // The PUBLIC IVirtualDesktopManager interface - only 3 methods exist
    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out bool onCurrentDesktop);
        
        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
        
        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }
    
    private static IVirtualDesktopManager? _manager;
    private static bool _initializationFailed = false;
    
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
    
    private static IVirtualDesktopManager? GetManager()
    {
        if (_initializationFailed)
            return null;
            
        if (_manager == null)
        {
            try
            {
                var type = Type.GetTypeFromCLSID(CLSID_VirtualDesktopManager);
                if (type == null)
                {
                    DebugLogger.Log("VirtualDesktopManager: COM type not found");
                    _initializationFailed = true;
                    return null;
                }
                
                var instance = Activator.CreateInstance(type);
                if (instance == null)
                {
                    DebugLogger.Log("VirtualDesktopManager: Failed to create COM instance");
                    _initializationFailed = true;
                    return null;
                }
                
                _manager = (IVirtualDesktopManager)instance;
                DebugLogger.Log("VirtualDesktopManager: Successfully initialized (public API only)");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"VirtualDesktopManager: Initialization failed: {ex.Message}");
                _initializationFailed = true;
                return null;
            }
        }
        return _manager;
    }
    
    /// <summary>
    /// Checks if a window is on the currently active virtual desktop
    /// </summary>
    public static bool IsWindowOnCurrentDesktop(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
                return true; // Assume on current desktop if we can't check
            
            var manager = GetManager();
            if (manager == null)
                return true;
            
            var hr = manager.IsWindowOnCurrentVirtualDesktop(hwnd, out bool onCurrent);
            if (hr == 0)
                return onCurrent;
            
            return true; // Assume on current desktop if call fails
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"VirtualDesktopManager: IsWindowOnCurrentDesktop failed: {ex.Message}");
            return true;
        }
    }
    
    /// <summary>
    /// Gets the virtual desktop ID that a window belongs to
    /// </summary>
    public static Guid GetWindowDesktopId(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
                return Guid.Empty;
            
            var manager = GetManager();
            if (manager == null)
                return Guid.Empty;
            
            var hr = manager.GetWindowDesktopId(hwnd, out Guid desktopId);
            if (hr == 0)
                return desktopId;
            
            return Guid.Empty;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"VirtualDesktopManager: GetWindowDesktopId failed: {ex.Message}");
            return Guid.Empty;
        }
    }
    
    /// <summary>
    /// Moves a window to a specific virtual desktop
    /// </summary>
    public static bool MoveWindowToDesktop(IntPtr hwnd, Guid desktopId)
    {
        try
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
                return false;
            
            if (desktopId == Guid.Empty)
                return false;
            
            var manager = GetManager();
            if (manager == null)
                return false;
            
            var hr = manager.MoveWindowToDesktop(hwnd, ref desktopId);
            return hr == 0;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"VirtualDesktopManager: MoveWindowToDesktop failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Returns true if the virtual desktop API is available
    /// </summary>
    public static bool IsAvailable => GetManager() != null;
}
