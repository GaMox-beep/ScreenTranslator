using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ScreenTranslator.Services.Hotkey;

public class GlobalHotkeyManager
{
    private const int HotkeyId = 9000;
    private const int WmHotkey = 0x0312;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    private IntPtr _windowHandle = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private bool _isRegistered;

    public event Action? HotkeyPressed;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public bool Register(Window window, string modifierStr, string keyStr)
    {
        Unregister();

        _windowHandle = new WindowInteropHelper(window).Handle;
        if (_windowHandle == IntPtr.Zero)
        {
            // Nếu Window chưa nạp handle, đợi Loaded
            return false;
        }

        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(HwndHook);

        var modifier = ParseModifier(modifierStr);
        var vk = ParseVirtualKey(keyStr);

        if (vk == 0) return false;

        _isRegistered = RegisterHotKey(_windowHandle, HotkeyId, modifier | ModNoRepeat, vk);
        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
            _isRegistered = false;
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource = null;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static uint ParseModifier(string modifier) => modifier switch
    {
        "Alt" => ModAlt,
        "Ctrl" => ModControl,
        "Shift" => ModShift,
        "Ctrl + Alt" => ModControl | ModAlt,
        "Ctrl + Shift" => ModControl | ModShift,
        "Alt + Shift" => ModAlt | ModShift,
        _ => ModAlt
    };

    private static uint ParseVirtualKey(string key)
    {
        if (key.Equals("` (Tilde)", StringComparison.OrdinalIgnoreCase))
        {
            return (uint)KeyInterop.VirtualKeyFromKey(Key.OemTilde);
        }

        if (Enum.TryParse<Key>(key, true, out var wpfKey))
        {
            return (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
        }

        return 0;
    }

}
