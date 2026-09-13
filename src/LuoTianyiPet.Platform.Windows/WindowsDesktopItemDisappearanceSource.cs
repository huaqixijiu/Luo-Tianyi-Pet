using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

/// <summary>
/// Observes only the user's physical Desktop folders and caches icon bounds
/// through public Windows UI Automation. It never reads file contents and does
/// not retain or log full paths.
/// </summary>
public sealed class WindowsDesktopItemDisappearanceSource : IDesktopItemDisappearanceSource
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentDictionary<string, PointerPoint> _positions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Timer _cacheTimer;
    private int _started;
    private int _refreshing;
    private bool _disposed;

    public WindowsDesktopItemDisappearanceSource() : this(null) { }

    internal WindowsDesktopItemDisappearanceSource(Action? readIcons)
    {
        _cacheTimer = new System.Threading.Timer(
            _ => { if (Volatile.Read(ref _started) != 0 && !_disposed) { if(readIcons != null) readIcons(); else RefreshIconPositions(); } },
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    public event EventHandler<DesktopItemDisappearedEventArgs>? ItemDisappeared;

    public void Start()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsDesktopItemDisappearanceSource));
        }
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        IEnumerable<string> folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string folder in folders)
        {
            FileSystemWatcher watcher = new(folder)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true,
            };
            watcher.Deleted += OnDeleted;
            _watchers.Add(watcher);
        }

        // UI Automation calls into Explorer and may block on another process. Even the
        // initial cache fill must run on the timer worker, never the WPF startup thread.
        _cacheTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(700));
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (Volatile.Read(ref _started) == 0)
        {
            return;
        }

        string fileName = Path.GetFileName(e.Name ?? string.Empty);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        bool cached = TryRemovePosition(fileName, out PointerPoint position) ||
            TryRemovePosition(stem, out position);
        if (!cached)
        {
            position = TryGetCursorPosition(out PointerPoint cursor)
                ? cursor
                : new PointerPoint(320, 240);
        }

        ItemDisappeared?.Invoke(
            this,
            new DesktopItemDisappearedEventArgs(position, cached));
    }

    private bool TryRemovePosition(string key, out PointerPoint position)
    {
        string normalized = NormalizeName(key);
        if (!string.IsNullOrEmpty(normalized) && _positions.TryRemove(normalized, out position))
        {
            return true;
        }

        position = default;
        return false;
    }

    private void RefreshIconPositions()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) != 0)
        {
            return;
        }

        try
        {
            AutomationElement? desktopList = FindDesktopList();
            if (desktopList is null)
            {
                return;
            }

            AutomationElementCollection items = desktopList.FindAll(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
            foreach (AutomationElement item in items)
            {
                string name = NormalizeName(item.Current.Name);
                System.Windows.Rect bounds = item.Current.BoundingRectangle;
                if (string.IsNullOrEmpty(name) || bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                {
                    continue;
                }

                _positions[name] = new PointerPoint(
                    bounds.Left + bounds.Width / 2,
                    bounds.Top + bounds.Height / 2);
            }
        }
        catch (ElementNotAvailableException)
        {
            // Explorer may be rebuilding the desktop view. The next refresh retries.
        }
        catch (InvalidOperationException)
        {
            // UI Automation can be temporarily unavailable during shell restart.
        }
        catch (COMException)
        {
            // An unavailable Explorer provider must not bring down the timer worker.
        }
        finally
        {
            Volatile.Write(ref _refreshing, 0);
        }
    }

    private static AutomationElement? FindDesktopList()
    {
        // Locate Explorer's desktop host through window classes, then ask UIA only
        // about that list. RootElement.Descendants also traverses every open app,
        // including our own WPF window, and can wait indefinitely on unrelated apps.
        nint list=0;
        EnumWindows((window,_)=>
        {
            nint view=FindWindowEx(window,0,"SHELLDLL_DefView",null);
            if(view!=0)list=FindWindowEx(view,0,"SysListView32",null);
            return list==0;
        },0);
        return list==0?null:AutomationElement.FromHandle(list);
    }

    private static string NormalizeName(string value) =>
        value.Trim().Normalize().ToUpperInvariant();

    private static bool TryGetCursorPosition(out PointerPoint point)
    {
        if (GetCursorPos(out NativePoint native))
        {
            point = new PointerPoint(native.X, native.Y);
            return true;
        }

        point = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        _cacheTimer.Dispose();
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        _cacheTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        foreach (FileSystemWatcher watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Deleted -= OnDeleted;
            watcher.Dispose();
        }
        _watchers.Clear();
        _positions.Clear();
    }

    private delegate bool EnumWindowCallback(nint window,nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowCallback callback,nint parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern nint FindWindowEx(nint parent,nint after,string className,string? title);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
