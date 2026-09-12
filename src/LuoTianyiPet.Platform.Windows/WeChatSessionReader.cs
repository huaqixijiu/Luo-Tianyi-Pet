using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

public sealed record WeChatSessionSnapshot(long WindowId, bool Foreground, bool Minimized,
    IReadOnlyList<WeChatSessionRow> Rows);

public static class WeChatSessionReader
{
    public static WeChatSessionSnapshot? TryRead()
    {
        WeChatSessionSnapshot? result = null;
        int candidates = 0;
        EnumWindows((window, _) =>
        {
            try
            {
                GetWindowThreadProcessId(window, out uint pid);
                using var process = Process.GetProcessById((int)pid);
                if (!string.Equals(process.ProcessName,"Weixin",StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(process.ProcessName,"WeChat",StringComparison.OrdinalIgnoreCase)) return true;
                if (++candidates>4) return false;
                var rows=ReadRows(AutomationElement.FromHandle(window));
                if(rows is not null) result=new((long)window,GetForegroundWindow()==window,IsIconic(window),rows);
            }
            catch (Exception) { /* Unavailable UIA or a closing client has no details. */ }
            return result is null;
        },IntPtr.Zero);
        return result;
    }
    private static IReadOnlyList<WeChatSessionRow>? ReadRows(AutomationElement root)
    {
        var clock=Stopwatch.StartNew();
        var walker=TreeWalker.RawViewWalker;
        var queue=new Queue<(AutomationElement Element,int Depth)>();queue.Enqueue((root,0));
        AutomationElement? list=null;int visited=0;
        while(queue.Count>0 && visited++<350 && clock.ElapsedMilliseconds<700)
        {
            var (element,depth)=queue.Dequeue();
            string id=element.Current.AutomationId;
            if(id=="session_list" && element.Current.ControlType==ControlType.List){list=element;break;}
            if(id=="chat_message_page" || id=="chat_message_list" || id=="chat_input_field" || depth>=22)continue;
            int children=0;
            for(var child=walker.GetFirstChild(element);child!=null && children++<60;child=walker.GetNextSibling(child))
                queue.Enqueue((child,depth+1));
        }
        if(list is null)return null;
        List<WeChatSessionRow> rows=[];int scanned=0;
        for(var child=walker.GetFirstChild(list);child!=null && scanned++<50;child=walker.GetNextSibling(child))
        {
            if(clock.ElapsedMilliseconds>1000)return null;
            var info=child.Current;
            if(info.ControlType!=ControlType.ListItem || !info.AutomationId.StartsWith("session_item_",StringComparison.Ordinal))continue;
            var row=WeChatSessionParser.Parse(info.AutomationId,info.Name);
            if(row is not null)rows.Add(row);
        }
        return rows.Count>0 ? rows : null;
    }
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumWindowsCallback callback,IntPtr param);
    private delegate bool EnumWindowsCallback(IntPtr window,IntPtr param);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern bool IsIconic(IntPtr window);
}

public sealed class WindowsWeChatSessionNotificationSource : IDisposable
{
    private readonly object _sync=new();
    private readonly WeChatSessionChangeTracker _tracker=new();
    private Timer? _timer;
    private int _reading;
    private long _window;
    private int _generation;
    private bool _started,_disposed;
    public event EventHandler<MessageNotificationReceivedEventArgs>? NotificationReceived;
    public void Start()
    {
        lock(_sync)
        {
            if(_disposed || _started)return;
            _started=true;_generation++;
            _timer ??=new Timer(_=>Poll(),null,Timeout.Infinite,Timeout.Infinite);
            _timer.Change(0,1500);
        }
    }
    public void Stop()
    {
        lock(_sync){if(_disposed)return;_started=false;_generation++;_timer?.Change(Timeout.Infinite,Timeout.Infinite);_tracker.Reset();_window=0;}
    }
    private void Poll()
    {
        if(Interlocked.Exchange(ref _reading,1)!=0)return;
        try
        {
            int generation;
            lock(_sync){if(!_started || _disposed)return;generation=_generation;}
            var snapshot=WeChatSessionReader.TryRead();
            lock(_sync)
            {
                if(!_started || _disposed || generation!=_generation)return;
                if(snapshot is null){_tracker.Reset();_window=0;return;}
                if(snapshot.WindowId!=_window){_tracker.Reset();_window=snapshot.WindowId;}
                foreach(var message in _tracker.Observe(snapshot.Rows,snapshot.Foreground,DateTimeOffset.Now))
                    NotificationReceived?.Invoke(this,new(message));
            }
        }
        catch(Exception){lock(_sync){_tracker.Reset();_window=0;}}
        finally{Volatile.Write(ref _reading,0);}
    }
    public void Dispose(){lock(_sync){if(_disposed)return;Stop();_disposed=true;_timer?.Dispose();}}
}
