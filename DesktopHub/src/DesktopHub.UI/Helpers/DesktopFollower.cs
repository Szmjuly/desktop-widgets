using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Monitors virtual desktop switches and moves tracked windows to the active desktop.
/// Uses the PUBLIC IVirtualDesktopManager API (IsWindowOnCurrentVirtualDesktop + MoveWindowToDesktop).
/// 
/// Strategy:
/// 1. Poll every 500ms to check if tracked windows are on the current desktop
/// 2. Use GetForegroundWindow() to find the current desktop ID (foreground window is always on active desktop)
/// 3. Use MoveWindowToDesktop() to move our windows there
/// 4. Apply a cooldown after moving to prevent re-detection loops
/// </summary>
public class DesktopFollower : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly List<WeakReference<Window>> _trackedWindows = new();
    private bool _isEnabled = false;
    private bool _disposed = false;
    
    // Cooldown: after moving windows, skip detection for this duration
    private DateTime _cooldownUntil = DateTime.MinValue;
    private const int CooldownMs = 2000;
    
    // Track the last desktop we moved to, to avoid redundant moves
    private Guid _lastMovedToDesktopId = Guid.Empty;
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    
    public DesktopFollower()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += OnTimerTick;
    }
    
    /// <summary>
    /// Start tracking and following desktop switches
    /// </summary>
    public void Start()
    {
        if (_disposed) return;
        
        if (!VirtualDesktopManager.IsAvailable)
        {
            DebugLogger.Log("DesktopFollower: Virtual desktop API not available, cannot follow desktops");
            return;
        }
        
        _isEnabled = true;
        _cooldownUntil = DateTime.UtcNow.AddMilliseconds(CooldownMs); // Initial cooldown to let windows settle
        _timer.Start();
        DebugLogger.Log("DesktopFollower: Started monitoring desktop switches");
    }
    
    /// <summary>
    /// Stop tracking desktop switches
    /// </summary>
    public void Stop()
    {
        _isEnabled = false;
        _timer.Stop();
        DebugLogger.Log("DesktopFollower: Stopped monitoring desktop switches");
    }
    
    /// <summary>
    /// Add a window to be tracked and moved across desktops
    /// </summary>
    public void TrackWindow(Window window)
    {
        if (window == null) return;
        
        CleanupDeadReferences();
        
        foreach (var weakRef in _trackedWindows)
        {
            if (weakRef.TryGetTarget(out var existing) && existing == window)
                return;
        }
        
        _trackedWindows.Add(new WeakReference<Window>(window));
        DebugLogger.Log($"DesktopFollower: Now tracking {window.GetType().Name} (total: {_trackedWindows.Count})");
    }
    
    /// <summary>
    /// Remove a window from tracking
    /// </summary>
    public void UntrackWindow(Window window)
    {
        if (window == null) return;
        
        _trackedWindows.RemoveAll(wr =>
        {
            if (wr.TryGetTarget(out var w))
                return w == window;
            return true;
        });
        
        DebugLogger.Log($"DesktopFollower: Untracked {window.GetType().Name} (remaining: {_trackedWindows.Count})");
    }
    
    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_isEnabled || _disposed) return;
        
        // Respect cooldown
        if (DateTime.UtcNow < _cooldownUntil) return;
        
        try
        {
            CleanupDeadReferences();
            if (_trackedWindows.Count == 0) return;
            
            // Check if any visible tracked window is NOT on the current desktop
            var windowsToMove = new List<(Window window, IntPtr hwnd)>();
            
            foreach (var weakRef in _trackedWindows)
            {
                if (!weakRef.TryGetTarget(out var window)) continue;
                if (window.Visibility != Visibility.Visible) continue;
                
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) continue;
                
                if (!VirtualDesktopManager.IsWindowOnCurrentDesktop(hwnd))
                {
                    windowsToMove.Add((window, hwnd));
                }
            }
            
            if (windowsToMove.Count == 0) return;
            
            // Get the current desktop ID from the foreground window
            var currentDesktopId = GetCurrentDesktopId();
            
            if (currentDesktopId == Guid.Empty)
            {
                DebugLogger.Log("DesktopFollower: Could not determine current desktop ID, skipping");
                return;
            }
            
            // Check if this is actually a new desktop (not just re-detecting the same state)
            if (currentDesktopId == _lastMovedToDesktopId)
            {
                // We already tried moving to this desktop - apply cooldown to avoid loop
                _cooldownUntil = DateTime.UtcNow.AddMilliseconds(CooldownMs);
                return;
            }
            
            DebugLogger.Log($"DesktopFollower: Desktop switch detected! Moving {windowsToMove.Count} window(s) to {currentDesktopId:B}");
            
            int successCount = 0;
            foreach (var (window, hwnd) in windowsToMove)
            {
                try
                {
                    var success = VirtualDesktopManager.MoveWindowToDesktop(hwnd, currentDesktopId);
                    if (success)
                    {
                        successCount++;
                        DebugLogger.Log($"DesktopFollower: Moved {window.GetType().Name} successfully");
                    }
                    else
                    {
                        DebugLogger.Log($"DesktopFollower: Failed to move {window.GetType().Name} (MoveWindowToDesktop returned false)");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"DesktopFollower: Error moving {window.GetType().Name}: {ex.Message}");
                }
            }
            
            _lastMovedToDesktopId = currentDesktopId;
            _cooldownUntil = DateTime.UtcNow.AddMilliseconds(CooldownMs);
            DebugLogger.Log($"DesktopFollower: Move complete ({successCount}/{windowsToMove.Count} succeeded), cooldown {CooldownMs}ms");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"DesktopFollower: Timer tick error: {ex.Message}");
            _cooldownUntil = DateTime.UtcNow.AddMilliseconds(CooldownMs);
        }
    }
    
    /// <summary>
    /// Gets the current virtual desktop ID by finding a window on the active desktop.
    /// The foreground window is always on the current desktop.
    /// </summary>
    private Guid GetCurrentDesktopId()
    {
        try
        {
            // Primary: use the foreground window
            var fgWindow = GetForegroundWindow();
            if (fgWindow != IntPtr.Zero)
            {
                // Make sure it's not one of our own tracked windows
                bool isOurWindow = false;
                foreach (var weakRef in _trackedWindows)
                {
                    if (weakRef.TryGetTarget(out var window))
                    {
                        var ourHwnd = new WindowInteropHelper(window).Handle;
                        if (ourHwnd == fgWindow)
                        {
                            isOurWindow = true;
                            break;
                        }
                    }
                }
                
                if (!isOurWindow)
                {
                    var id = VirtualDesktopManager.GetWindowDesktopId(fgWindow);
                    if (id != Guid.Empty)
                        return id;
                }
            }
            
            // Fallback: enumerate visible top-level windows to find one with a valid desktop ID
            Guid foundId = Guid.Empty;
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd)) return true; // continue
                
                // Skip our own windows
                foreach (var weakRef in _trackedWindows)
                {
                    if (weakRef.TryGetTarget(out var window))
                    {
                        var ourHwnd = new WindowInteropHelper(window).Handle;
                        if (ourHwnd == hwnd) return true; // continue
                    }
                }
                
                // Check if this window is on the current desktop
                if (VirtualDesktopManager.IsWindowOnCurrentDesktop(hwnd))
                {
                    var id = VirtualDesktopManager.GetWindowDesktopId(hwnd);
                    if (id != Guid.Empty)
                    {
                        foundId = id;
                        return false; // stop enumeration
                    }
                }
                
                return true; // continue
            }, IntPtr.Zero);
            
            if (foundId != Guid.Empty)
                return foundId;
            
            // Last resort: create a temporary probe window to detect the current desktop.
            // New windows are always created on the active virtual desktop.
            return GetDesktopIdViaProbeWindow();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"DesktopFollower: GetCurrentDesktopId failed: {ex.Message}");
            return Guid.Empty;
        }
    }
    
    /// <summary>
    /// Creates a temporary invisible window to detect the current virtual desktop ID.
    /// New windows are always created on the active virtual desktop, so this works
    /// even on empty desktops where no other app windows exist to query.
    /// </summary>
    private Guid GetDesktopIdViaProbeWindow()
    {
        try
        {
            var probeWindow = new Window
            {
                Width = 1,
                Height = 1,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Left = -32000,
                Top = -32000,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Opacity = 0
            };
            probeWindow.Show();
            try
            {
                var probeHwnd = new WindowInteropHelper(probeWindow).Handle;
                if (probeHwnd != IntPtr.Zero)
                {
                    var id = VirtualDesktopManager.GetWindowDesktopId(probeHwnd);
                    if (id != Guid.Empty)
                    {
                        DebugLogger.Log($"DesktopFollower: Detected desktop via probe window: {id:B}");
                        return id;
                    }
                }
            }
            finally
            {
                probeWindow.Close();
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"DesktopFollower: Probe window fallback failed: {ex.Message}");
        }
        
        return Guid.Empty;
    }
    
    private void CleanupDeadReferences()
    {
        _trackedWindows.RemoveAll(wr => !wr.TryGetTarget(out _));
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _trackedWindows.Clear();
    }
}
