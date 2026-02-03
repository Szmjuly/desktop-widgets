using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Global hotkey registration using Win32 RegisterHotKey API (high performance)
/// </summary>
public class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly Window _window;
    private readonly uint _modifiers;
    private readonly uint _key;
    private readonly int _hotkeyId;
    private HwndSource? _source;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;
    public Func<bool>? ShouldSuppressHotkey;

    // Win32 API imports
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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
        _hotkeyId = new Random().Next(0x0000, 0xBFFF);

        var helper = new WindowInteropHelper(window);
        var handle = helper.Handle;

        if (handle == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(window).Handle;
                RegisterHotkeyInternal(h);
            };
        }
        else
        {
            RegisterHotkeyInternal(handle);
        }
    }

    private void RegisterHotkeyInternal(IntPtr handle)
    {
        _source = HwndSource.FromHwnd(handle);
        _source.AddHook(WndProc);

        if (!RegisterHotKey(handle, _hotkeyId, _modifiers, _key))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to register hotkey. Error code: {error}");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
        {
            var shouldSuppress = ShouldSuppressHotkey?.Invoke() ?? false;
            if (!shouldSuppress)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_source != null)
        {
            var handle = _source.Handle;
            if (handle != IntPtr.Zero)
            {
                UnregisterHotKey(handle, _hotkeyId);
            }
            _source.RemoveHook(WndProc);
        }

        GC.SuppressFinalize(this);
    }

    ~GlobalHotkey()
    {
        Dispose();
    }
}
