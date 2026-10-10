using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenTranslator.Services.Capture;

/// <summary>Positions native windows using physical desktop pixels, including negative monitor origins.</summary>
internal static class ScreenCoordinates
{
    private const uint NoSize = 0x0001;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    public static void CoverVirtualDesktop(Window window)
    {
        // SM_X/Y/CX/CYVIRTUALSCREEN return physical pixels in a PerMonitorV2 process.
        var hwnd = new WindowInteropHelper(window).Handle;
        Move(hwnd, GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79), NoZOrder | NoActivate);
    }

    public static void PlaceNear(Window window, Point pixelPosition)
    {
        var monitor = MonitorFromPoint(new NativePoint { X = (int)pixelPosition.X, Y = (int)pixelPosition.Y }, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var hwnd = new WindowInteropHelper(window).Handle;

        // Move onto the target monitor first so WPF applies that monitor's DPI to the popup.
        Move(hwnd, info.Work.Left, info.Work.Top, 0, 0, NoSize | NoZOrder | NoActivate);
        if (!GetWindowRect(hwnd, out var bounds)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        var x = Math.Clamp((int)pixelPosition.X + 10, info.Work.Left, Math.Max(info.Work.Left, info.Work.Right - width));
        var y = Math.Clamp((int)pixelPosition.Y + 15, info.Work.Top, Math.Max(info.Work.Top, info.Work.Bottom - height));
        Move(hwnd, x, y, 0, 0, NoSize | NoZOrder | NoActivate);
    }

    private static void Move(IntPtr hwnd, int x, int y, int width, int height, uint flags)
    {
        if (!SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, flags))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
