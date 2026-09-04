using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LuoTianyiPet.App;

/// <summary>
/// Keeps a desktop companion out of task switchers while allowing it to remain
/// visible when Windows invokes "Show desktop".
/// </summary>
internal sealed class DesktopToolWindowBehavior : IDisposable
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const int WmSysCommand = 0x0112;
    private const long ScCommandMask = 0xFFF0L;
    private const long ScMinimize = 0xF020L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    private readonly Window _window;
    private readonly bool _keepVisibleOnShowDesktop;
    private HwndSource? _source;
    private bool _restoreScheduled;
    private bool _disposed;

    public DesktopToolWindowBehavior(Window window, bool keepVisibleOnShowDesktop)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _keepVisibleOnShowDesktop = keepVisibleOnShowDesktop;
        _window.ShowInTaskbar = false;
        _window.ShowActivated = false;
        _window.SourceInitialized += OnSourceInitialized;
        _window.StateChanged += OnWindowStateChanged;
    }

    public bool IsToolWindowStyleApplied { get; private set; }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        nint handle = new WindowInteropHelper(_window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        nint currentStyle = GetWindowLongPtr(handle, GwlExStyle);
        long desiredStyleValue =
            (currentStyle.ToInt64() | WsExToolWindow) & ~WsExAppWindow;
        nint desiredStyle = new(desiredStyleValue);
        if (desiredStyle != currentStyle)
        {
            _ = SetWindowLongPtr(handle, GwlExStyle, desiredStyle);
            _ = SetWindowPos(
                handle,
                nint.Zero,
                0,
                0,
                0,
                0,
                SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }

        nint verifiedStyle = GetWindowLongPtr(handle, GwlExStyle);
        IsToolWindowStyleApplied =
            (verifiedStyle.ToInt64() & WsExToolWindow) != 0 &&
            (verifiedStyle.ToInt64() & WsExAppWindow) == 0;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WindowMessageHook);
    }

    private nint WindowMessageHook(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (_keepVisibleOnShowDesktop &&
            message == WmSysCommand &&
            (wParam.ToInt64() & ScCommandMask) == ScMinimize)
        {
            handled = true;
        }

        return nint.Zero;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (!_keepVisibleOnShowDesktop ||
            _disposed ||
            _restoreScheduled ||
            _window.WindowState != WindowState.Minimized)
        {
            return;
        }

        _restoreScheduled = true;
        _window.Dispatcher.BeginInvoke(
            DispatcherPriority.Send,
            () =>
            {
                try
                {
                    if (!_disposed && _window.WindowState == WindowState.Minimized)
                    {
                        _window.WindowState = WindowState.Normal;
                    }
                }
                finally
                {
                    _restoreScheduled = false;
                }
            });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.SourceInitialized -= OnSourceInitialized;
        _window.StateChanged -= OnWindowStateChanged;
        if (_source is { IsDisposed: false })
        {
            _source.RemoveHook(WindowMessageHook);
        }
        _source = null;
    }

    private static nint GetWindowLongPtr(nint windowHandle, int index) =>
        nint.Size == 8
            ? GetWindowLongPtr64(windowHandle, index)
            : new nint(GetWindowLong32(windowHandle, index));

    private static nint SetWindowLongPtr(nint windowHandle, int index, nint value) =>
        nint.Size == 8
            ? SetWindowLongPtr64(windowHandle, index, value)
            : new nint(SetWindowLong32(windowHandle, index, value.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint windowHandle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint windowHandle, int index, nint value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
