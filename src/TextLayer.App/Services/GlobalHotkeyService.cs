using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace TextLayer.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly Dictionary<int, Action> callbacks = new();
    private HwndSource? source;
    private IntPtr windowHandle;
    private int nextHotkeyId = 1;

    public void Attach(Window window)
    {
        if (source is not null)
        {
            return;
        }

        windowHandle = new WindowInteropHelper(window).EnsureHandle();
        source = HwndSource.FromHwnd(windowHandle) ?? throw new InvalidOperationException("TextLayer could not create a message source for global hotkeys.");
        source.AddHook(WndProc);
    }

    public int Register(ModifierKeys modifiers, Key key, Action callback)
    {
        if (source is null || windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Attach the hotkey service to a window before registering hotkeys.");
        }

        var id = nextHotkeyId++;
        if (!NativeMethods.RegisterHotKey(windowHandle, id, (uint)modifiers, (uint)KeyInterop.VirtualKeyFromKey(key)))
        {
            throw new InvalidOperationException("TextLayer could not register the global OCR hotkey. Another app may already be using it.");
        }

        callbacks[id] = callback;
        return id;
    }

    public void Dispose()
    {
        if (windowHandle != IntPtr.Zero)
        {
            foreach (var id in callbacks.Keys)
            {
                NativeMethods.UnregisterHotKey(windowHandle, id);
            }
        }

        callbacks.Clear();

        if (source is not null)
        {
            source.RemoveHook(WndProc);
            source = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY &&
            callbacks.TryGetValue(wParam.ToInt32(), out var callback))
        {
            callback();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static class NativeMethods
    {
        internal const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
