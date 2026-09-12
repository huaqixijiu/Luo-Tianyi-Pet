using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Text.RegularExpressions;

// A bounded, read-only diagnostic of public session fields. Never emits raw names or preview text.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var hwnd = new IntPtr(long.Parse(args[0]));
        GetWindowThreadProcessId(hwnd, out uint pid);
        using var process = Process.GetProcessById((int)pid);
        if (!string.Equals(process.ProcessName, "Weixin", StringComparison.OrdinalIgnoreCase)) return;
        Console.WriteLine($"Visible={IsWindowVisible(hwnd)}; Minimized={IsIconic(hwnd)}; Foreground={GetForegroundWindow()==hwnd}; Version={FileVersionInfo.GetVersionInfo(@"C:\Program Files\Tencent\Weixin\Weixin.exe").FileVersion}");
        var root = AutomationElement.FromHandle(hwnd);
        var walker = TreeWalker.RawViewWalker;
        var queue = new Queue<(AutomationElement Element,int Depth)>(); queue.Enqueue((root,0));
        AutomationElement? list = null; int visited=0;
        while(queue.Count>0 && visited++<350)
        {
            var (element,depth)=queue.Dequeue();
            string id = element.Current.AutomationId;
            if(id=="session_list") {list=element;break;}
            // Exclude the chat view entirely; never traverse message history or input fields.
            if(id=="chat_message_page" || id=="chat_message_list" || id=="chat_input_field" || depth>=22) continue;
            int children=0;
            for(var c=walker.GetFirstChild(element); c!=null && children++<60;c=walker.GetNextSibling(c)) queue.Enqueue((c,depth+1));
        }
        Console.WriteLine($"SessionListFound={list!=null}; Visited={visited}");
        if(list==null)return;
        int row=0;
        for(var c=walker.GetFirstChild(list);c!=null && row<50;c=walker.GetNextSibling(c))
        {
            var info=c.Current;
            if(!info.AutomationId.StartsWith("session_item_",StringComparison.Ordinal))continue;
            string name=info.Name;
            string[] lines=name.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
            // Parse only the standalone badge line, never a number embedded in a preview or nickname.
            var badge=lines.Select(x=>Regex.Match(x.Trim(),@"^\[(\d+)(\+?)条\]$")).FirstOrDefault(m=>m.Success);
            var inline=Regex.Match(name,@"\[(\d+)(\+?)条\]");
            string shape=string.Join(";",lines.Select((line,i)=>$"L{i}:len={line.Length},badgeAtStart={Regex.IsMatch(line,@"^\[\d+\+?条\]")},badgeOnly={Regex.IsMatch(line,@"^\[\d+\+?条\]$")}"));
            bool selected=c.TryGetCurrentPattern(SelectionItemPattern.Pattern,out object pattern) && ((SelectionItemPattern)pattern).Current.IsSelected;
            Console.WriteLine($"Row={++row}; Lines={lines.Length}; NameLength={name.Length}; StandaloneBadge={(badge?.Success==true?badge.Groups[1].Value+badge.Groups[2].Value:"unknown")}; InlineBadge={(inline.Success?inline.Groups[1].Value+inline.Groups[2].Value:"unknown")}; Shape={shape}; Offscreen={info.IsOffscreen}; Selected={selected}; Muted={lines.Any(x=>x.Trim()=="消息免打扰")}");
        }
    }
    [DllImport("user32.dll")]private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
}
