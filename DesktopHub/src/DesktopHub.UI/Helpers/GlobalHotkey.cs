using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Global hotkey registration using Win32 API
/// </summary>
public class GlobalHotkey : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private readonly Window _window;
    private readonly uint _modifiers;
    private readonly uint _key;
    private HwndSource? _source;
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;
    private bool _hotkeyPressed;
    private bool _disposed;
    private bool _suppressingHotkeyEvents;

    public event EventHandler? HotkeyPressed;
    public Func<bool>? ShouldSuppressHotkey;

    // Win32 API imports
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // Modifier keys
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    public GlobalHotkey(Window window, uint modifiers, uint key)
    {
        _window = window;
        _modifiers = modifiers;
        _key = key;

        // Get window handle
        var helper = new WindowInteropHelper(window);
        var handle = helper.Handle;

        if (handle == IntPtr.Zero)
        {
            // Window not loaded yet, wait for it
            window.SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(window).Handle;
                RegisterHookInternal(h);
            };
        }
        else
        {
            RegisterHookInternal(handle);
        }
    }

    private void RegisterHookInternal(IntPtr handle)
    {
        _source = HwndSource.FromHwnd(handle);
        _hookProc = HookCallback;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle("user32"), 0);
        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install keyboard hook for hotkey.");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = wParam.ToInt32();
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            
            if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
            {
                if (!_hotkeyPressed && IsHotkeyMatch(data.vkCode))
                {
                    // Check if hotkey should be suppressed
                    var shouldSuppress = ShouldSuppressHotkey?.Invoke() ?? false;
                    
                    if (!shouldSuppress)
                    {
                        _hotkeyPressed = true;
                        _suppressingHotkeyEvents = true;
                        HotkeyPressed?.Invoke(this, EventArgs.Empty);
                        
                        // Block the keyboard event to prevent character passthrough
                        return (IntPtr)1;
                    }
                }
            }
            else if (message == WM_KEYUP || message == WM_SYSKEYUP)
            {
                if (data.vkCode == _key)
                {
                    _hotkeyPressed = false;
                    
                    // Also block the key-up event if we blocked the key-down
                    if (_suppressingHotkeyEvents)
                    {
                        _suppressingHotkeyEvents = false;
                        return (IntPtr)1;
                    }
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool IsHotkeyMatch(uint vkCode)
    {
        if (vkCode != _key)
        {
            return false;
        }

        uint currentMods = 0;
        if (IsKeyDown(VK_CONTROL)) currentMods |= MOD_CONTROL;
        if (IsKeyDown(VK_MENU)) currentMods |= MOD_ALT;
        if (IsKeyDown(VK_SHIFT)) currentMods |= MOD_SHIFT;
        if (IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN)) currentMods |= MOD_WIN;

        return currentMods == _modifiers;
    }

    private static bool IsKeyDown(int vKey)
    {
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~GlobalHotkey()
    {
        Dispose();
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
