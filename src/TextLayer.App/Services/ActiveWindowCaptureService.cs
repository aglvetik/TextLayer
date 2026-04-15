using System.Runtime.InteropServices;
using TextLayer.App.Models;
using TextLayer.Domain.Geometry;

namespace TextLayer.App.Services;

public sealed class ActiveWindowCaptureService(ScreenGeometryService screenGeometryService)
{
    public nint GetForegroundWindowHandle() => NativeMethods.GetForegroundWindow();

    public Task<ScreenSelectionResult?> CaptureForegroundWindowBoundsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var handle = NativeMethods.GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return Task.FromResult<ScreenSelectionResult?>(null);
        }

        var bounds = GetWindowBounds(handle);
        if (bounds.IsEmpty)
        {
            return Task.FromResult<ScreenSelectionResult?>(null);
        }

        var monitor = screenGeometryService.GetMonitorFromPixelRect(bounds);
        return Task.FromResult<ScreenSelectionResult?>(new ScreenSelectionResult(bounds, monitor, handle));
    }

    private static PixelRect GetWindowBounds(IntPtr handle)
    {
        if (NativeMethods.DwmGetWindowAttribute(handle, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out var rect, Marshal.SizeOf<NativeMethods.RECT>()) == 0)
        {
            return ToPixelRect(rect);
        }

        return NativeMethods.GetWindowRect(handle, out rect)
            ? ToPixelRect(rect)
            : default;
    }

    private static PixelRect ToPixelRect(NativeMethods.RECT rect)
        => new(rect.Left, rect.Top, Math.Max(0, rect.Right - rect.Left), Math.Max(0, rect.Bottom - rect.Top));

    private static class NativeMethods
    {
        internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
