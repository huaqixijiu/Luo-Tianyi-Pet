using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

public sealed class PollingProtectedGameProcessMonitor : IProtectedGameProcessMonitor
{
    private readonly string[] _processNames;
    private readonly ProtectedGamePresenceTracker _tracker;
    private readonly TimeSpan _pollInterval;
    private readonly object _sync = new();
    private Dictionary<uint, string> _knownProcesses = [];
    private Timer? _timer;
    private bool _started;
    private bool _disposed;

    public PollingProtectedGameProcessMonitor(
        IEnumerable<string> processNames,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(processNames);
        _processNames = processNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => Path.GetFileNameWithoutExtension(name.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _tracker = new ProtectedGamePresenceTracker(_processNames);
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
        if (_pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }
    }

    public event EventHandler<ProtectedGamePresenceChangedEventArgs>? PresenceChanged;

    public bool IsRunning => _tracker.IsRunning;

    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            Dictionary<uint, string>? initial = TryEnumerateTargetProcesses();
            if (initial is not null)
            {
                foreach ((uint processId, string processName) in initial)
                {
                    _tracker.Seed(processName, processId);
                }

                _knownProcesses = initial;
            }

            _timer = new Timer(
                static state => ((PollingProtectedGameProcessMonitor)state!).Poll(),
                this,
                _pollInterval,
                _pollInterval);
            _started = true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_sync)
        {
            _disposed = true;
            _started = false;
            _timer?.Dispose();
            _timer = null;
            _knownProcesses.Clear();
        }

        GC.SuppressFinalize(this);
    }

    private void Poll()
    {
        Dictionary<uint, string>? current = TryEnumerateTargetProcesses();
        if (current is null)
        {
            return;
        }

        ProtectedGamePresenceTransition transition = ProtectedGamePresenceTransition.None;
        lock (_sync)
        {
            if (_disposed || !_started)
            {
                return;
            }

            foreach ((uint processId, string processName) in current)
            {
                if (!_knownProcesses.ContainsKey(processId) &&
                    _tracker.ObserveStarted(processName, processId) ==
                        ProtectedGamePresenceTransition.BecameRunning)
                {
                    transition = ProtectedGamePresenceTransition.BecameRunning;
                }
            }

            foreach ((uint processId, string processName) in _knownProcesses)
            {
                if (!current.ContainsKey(processId) &&
                    _tracker.ObserveStopped(processName, processId) ==
                        ProtectedGamePresenceTransition.BecameStopped)
                {
                    transition = ProtectedGamePresenceTransition.BecameStopped;
                }
            }

            _knownProcesses = current;
        }

        if (transition != ProtectedGamePresenceTransition.None)
        {
            PresenceChanged?.Invoke(
                this,
                new ProtectedGamePresenceChangedEventArgs(
                    transition == ProtectedGamePresenceTransition.BecameRunning,
                    DateTimeOffset.Now));
        }
    }

    private Dictionary<uint, string>? TryEnumerateTargetProcesses()
    {
        Dictionary<uint, string> matches = [];
        try
        {
            foreach (string processName in _processNames)
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    using (process)
                    {
                        matches[checked((uint)process.Id)] = processName;
                    }
                }
            }

            return matches;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Fail closed and keep the last known presence. The next timer tick retries.
            return null;
        }
    }
}

public sealed class WindowsForegroundApplicationProbe : IForegroundApplicationProbe
{
    public ForegroundApplicationSnapshot Query()
    {
        nint window = DesktopNativeMethods.GetForegroundWindow();
        if (window == 0)
        {
            return new(true, null, false);
        }

        if (DesktopNativeMethods.GetWindowThreadProcessId(window, out uint processId) == 0 ||
            processId == 0 ||
            !TryGetWindowMonitor(window, out NativeRectangle windowBounds, out NativeMonitorInfo monitorInfo))
        {
            return new(false, null, false);
        }

        try
        {
            using Process process = Process.GetProcessById(checked((int)processId));
            bool fullscreen = CoversMonitor(windowBounds, monitorInfo.Monitor);
            return new(true, process.ProcessName, fullscreen);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return new(false, null, false);
        }
    }

    internal static bool TryGetWindowMonitor(
        nint window,
        out NativeRectangle windowBounds,
        out NativeMonitorInfo monitorInfo)
    {
        windowBounds = default;
        monitorInfo = new NativeMonitorInfo
        {
            Size = (uint)Marshal.SizeOf<NativeMonitorInfo>(),
        };
        nint monitor = DesktopNativeMethods.MonitorFromWindow(
            window,
            DesktopNativeMethods.MonitorDefaultToNearest);
        return monitor != 0 &&
            DesktopNativeMethods.GetWindowRect(window, out windowBounds) &&
            DesktopNativeMethods.GetMonitorInfo(monitor, ref monitorInfo);
    }

    internal static bool CoversMonitor(NativeRectangle window, NativeRectangle monitor)
    {
        const int tolerance = 2;
        return window.Left <= monitor.Left + tolerance &&
            window.Top <= monitor.Top + tolerance &&
            window.Right >= monitor.Right - tolerance &&
            window.Bottom >= monitor.Bottom - tolerance;
    }
}

public sealed class WindowsWindowWorkAreaProvider : IWindowWorkAreaProvider
{
    public DesktopRectangle GetForWindow(nint windowHandle)
    {
        if (!WindowsForegroundApplicationProbe.TryGetWindowMonitor(
            windowHandle,
            out _,
            out NativeMonitorInfo monitorInfo))
        {
            throw new InvalidOperationException("Unable to determine the pet monitor work area.");
        }

        NativeRectangle work = monitorInfo.Work;
        return new DesktopRectangle(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top);
    }
}

public static class WindowsWindowZOrder
{
    public static bool SetTopmostWithoutActivation(nint windowHandle, bool topmost)
    {
        if (windowHandle == 0)
        {
            return false;
        }

        return DesktopNativeMethods.SetWindowPos(
            windowHandle,
            topmost ? DesktopNativeMethods.Topmost : DesktopNativeMethods.NotTopmost,
            0,
            0,
            0,
            0,
            DesktopNativeMethods.NoMove |
            DesktopNativeMethods.NoSize |
            DesktopNativeMethods.NoActivate |
            DesktopNativeMethods.NoOwnerZOrder);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRectangle
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMonitorInfo
{
    public uint Size;
    public NativeRectangle Monitor;
    public NativeRectangle Work;
    public uint Flags;
}

internal static class DesktopNativeMethods
{
    public static readonly nint Topmost = new(-1);
    public static readonly nint NotTopmost = new(-2);
    public const uint MonitorDefaultToNearest = 2;
    public const uint NoSize = 0x0001;
    public const uint NoMove = 0x0002;
    public const uint NoActivate = 0x0010;
    public const uint NoOwnerZOrder = 0x0200;

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(nint window, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    public static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(nint monitor, ref NativeMonitorInfo monitorInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
