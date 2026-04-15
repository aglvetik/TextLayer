using System.Runtime.InteropServices;
using System.Windows.Forms;
using TextLayer.App.Models;
using TextLayer.Domain.Geometry;

namespace TextLayer.App.Services;

public sealed class ScreenGeometryService
{
    public IReadOnlyList<MonitorInfo> GetAllMonitors()
        => Screen.AllScreens.Select(CreateMonitorInfo).ToArray();

    public MonitorInfo GetMonitorFromPixelRect(PixelRect pixelRect)
    {
        var centerX = pixelRect.X + Math.Max(0, pixelRect.Width / 2);
        var centerY = pixelRect.Y + Math.Max(0, pixelRect.Height / 2);
        return GetMonitorFromPixelPoint(centerX, centerY);
    }

    public MonitorInfo GetMonitorFromPixelPoint(int x, int y)
        => CreateMonitorInfo(Screen.FromPoint(new System.Drawing.Point(x, y)));

    private static MonitorInfo CreateMonitorInfo(Screen screen)
    {
        var bounds = screen.Bounds;
        var pixelBounds = new PixelRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        var monitorHandle = NativeMethods.MonitorFromPoint(
            new NativeMethods.POINT(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2)),
            NativeMethods.MONITOR_DEFAULTTONEAREST);

        var dpiScale = GetPixelsPerDip(monitorHandle);
        return new MonitorInfo(screen.DeviceName, pixelBounds, dpiScale.X, dpiScale.Y);
    }

    private static (double X, double Y) GetPixelsPerDip(IntPtr monitorHandle)
    {
        if (monitorHandle != IntPtr.Zero &&
            NativeMethods.GetDpiForMonitor(monitorHandle, NativeMethods.MonitorDpiType.Effective, out var dpiX, out var dpiY) == 0)
        {
            return (dpiX / 96d, dpiY / 96d);
        }

        return (1d, 1d);
    }

    private static class NativeMethods
    {
        internal const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("Shcore.dll")]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

        internal enum MonitorDpiType
        {
            Effective = 0,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct POINT(int x, int y)
        {
            public readonly int X = x;
            public readonly int Y = y;
        }
    }
}
