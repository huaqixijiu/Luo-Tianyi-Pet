using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows.Threading;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

internal static class Program
{
    private static readonly string Output = Path.Combine(AppContext.BaseDirectory, "result.txt");
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly HashSet<uint> Seen = [];
    private static readonly MessageProviderMatcher Matcher = new(new MessageNotificationPreferences());
    private static int _samples, _qqToasts, _shellFlashes, _detailSamples, _failures;
    private static bool _busy;
    private static uint _shellMessage;
    private static string _lastWindows = "";
    private static readonly object LogLock = new();

    [STAThread]
    private static void Main()
    {
        File.WriteAllText(Output, "QQ background probe: counts and field lengths only; no body reads, mouse movement or notification changes.\n");
        try
        {
            var access = UserNotificationListener.Current.GetAccessStatus();
            Log($"Access={access}");
            if (access != UserNotificationListenerAccessStatus.Allowed) return;
            using var production = new WindowsMessageNotificationSource(Matcher);
            production.NotificationReceived += (_, e) => Log($"ProductionReceived Provider={e.Provider}; TitleLength={e.Notification.ConversationDisplayName?.Length ?? 0}");
            Log($"ProductionAccess={production.GetAccessStatus()}");
            production.Start();
            using HwndSource source = new(new HwndSourceParameters("QqBackgroundProbe")
            { Width = 0, Height = 0, WindowStyle = 0, ExtendedWindowStyle = 0x08000080 });
            _shellMessage = RegisterWindowMessage("SHELLHOOK");
            source.AddHook(OnMessage);
            Log($"ShellRegistered={RegisterShellHookWindow(source.Handle)}");
            DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(750) };
            timer.Tick += async (_, _) =>
            {
                if (Clock.Elapsed.TotalSeconds > 300 || File.Exists(Path.Combine(AppContext.BaseDirectory, "stop")))
                {
                    Log($"Finished Samples={_samples}; QqToasts={_qqToasts}; ShellFlashes={_shellFlashes}; DetailSamples={_detailSamples}; Failures={_failures}");
                    timer.Stop(); DeregisterShellHookWindow(source.Handle); Dispatcher.CurrentDispatcher.InvokeShutdown(); return;
                }
                if (_busy) return;
                _busy = true;
                try { await SampleAsync(); }
                catch (Exception e) { _failures++; Log($"Failure Type={e.GetType().Name}; HResult={e.HResult:X8}"); }
                finally { _busy = false; }
            };
            timer.Start(); Dispatcher.Run();
        }
        catch (Exception e) { Log($"StartupFailure Type={e.GetType().Name}; HResult={e.HResult:X8}"); }
    }

    private static async Task SampleAsync()
    {
        var notifications = await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast);
        foreach (var notification in notifications)
        {
            if (Matcher.Identify(notification.AppInfo.AppUserModelId, notification.AppInfo.DisplayInfo.DisplayName) != MessageProvider.Qq || !Seen.Add(notification.Id)) continue;
            var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric) ?? notification.Notification.Visual.Bindings.FirstOrDefault();
            var elements = binding?.GetTextElements();
            // A single element may be body text: do not even read it.
            int titleLength = elements is { Count: >= 2 } ? elements[0].Text?.Length ?? 0 : 0;
            _qqToasts++;
            Log($"QqToast Baseline={_samples == 0}; TextElementCount={elements?.Count ?? 0}; TitleLength={titleLength}; QqForeground={IsQq(GetForegroundWindow())}");
        }
        string windows = DescribeQqPopups();
        if (_lastWindows != windows) { Log("QqPopupWindows=" + windows); _lastWindows = windows; }
        var details = await Task.Run(QqTrayDetailsReader.TryRead);
        if (details is not null) { _detailSamples++; Log($"PublicCardDetails NameLength={details.DisplayName.Length}; Count={details.UnreadCount}; QqForeground={IsQq(GetForegroundWindow())}"); }
        if (++_samples == 1) Log($"READY SnapshotCount={notifications.Count}; QqBaseline={_qqToasts}; QqForeground={IsQq(GetForegroundWindow())}");
        if (_samples % 20 == 0) Log($"Progress Samples={_samples}; QqToasts={_qqToasts}; ShellFlashes={_shellFlashes}; DetailSamples={_detailSamples}; QqForeground={IsQq(GetForegroundWindow())}");
    }

    private static nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)message == _shellMessage && (long)wParam == 0x8006 && IsQq(lParam))
        { _shellFlashes++; Log($"QqShellFlash QqForeground={IsQq(GetForegroundWindow())}"); }
        return 0;
    }

    private static bool IsQq(nint hwnd)
    {
        try { GetWindowThreadProcessId(hwnd, out uint pid); using var process = Process.GetProcessById((int)pid); return Matcher.IdentifyProcess(process.ProcessName) == MessageProvider.Qq; }
        catch { return false; }
    }

    private static string DescribeQqPopups()
    {
        List<string> windows = [];
        EnumWindows((hwnd, _) =>
        {
            if (!IsQq(hwnd) || !GetWindowRect(hwnd, out Rect r)) return true;
            double scale = Math.Max(96, GetDpiForWindow(hwnd)) / 96d;
            double width = (r.Right - r.Left) / scale, height = (r.Bottom - r.Top) / scale;
            if (width < 250 || width > 310 || height < 90 || height > 720) return true;
            var type = new StringBuilder(128); GetClassName(hwnd, type, type.Capacity);
            windows.Add($"{type}:{width:0}x{height:0}:Visible={IsWindowVisible(hwnd)}");
            return true;
        }, 0);
        return windows.Count == 0 ? "none" : string.Join(";", windows);
    }

    private static void Log(string text)
    {
        lock (LogLock) File.AppendAllText(Output, $"{Clock.Elapsed.TotalSeconds:0.0}s {text}\n");
    }
    private delegate bool Callback(nint hwnd, nint parameter);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool RegisterShellHookWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool DeregisterShellHookWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int length);
}
