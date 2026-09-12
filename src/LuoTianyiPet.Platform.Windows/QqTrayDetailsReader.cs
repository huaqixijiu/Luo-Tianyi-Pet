using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

public static class QqTrayDetailsReader
{
    // QQ's public tray card only. Never visits chat/main windows, reads preview containers,
    // expands a tray icon, or queries image URLs. No persistent cache or diagnostic text values.
    public static QqTrayDetails? TryRead()
    {
        QqTrayDetails? result = null;
        int candidates = 0;
        EnumWindows((hwnd, _) =>
        {
            try
            {
                if (!IsWindowVisible(hwnd) || !GetWindowRect(hwnd, out NativeRect rect)) return true;
                uint dpi = GetDpiForWindow(hwnd);
                double scale = dpi > 0 ? dpi / 96d : 1;
                double width = (rect.Right - rect.Left) / scale;
                double height = (rect.Bottom - rect.Top) / scale;
                if (width < 250 || width > 310 || height < 90 || height > 720) return true;
                StringBuilder windowClass = new(128);
                GetClassName(hwnd, windowClass, windowClass.Capacity);
                if (windowClass.ToString() != "Chrome_WidgetWin_1") return true;
                GetWindowThreadProcessId(hwnd, out uint pid);
                using Process process = Process.GetProcessById((int)pid);
                if (!string.Equals(process.ProcessName, "QQ", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(process.ProcessName, "QQNT", StringComparison.OrdinalIgnoreCase)) return true;
                if (++candidates > 4) return false;
                result = ReadCard(AutomationElement.FromHandle(hwnd), rect, scale);
            }
            catch (Exception exception) when (exception is COMException or ElementNotAvailableException or
                InvalidOperationException or ArgumentException or UnauthorizedAccessException or
                System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // Closed window, denied access or incompatible client: source-only notification.
            }
            return result is null;
        }, 0);
        return result;
    }

    private static QqTrayDetails? ReadCard(AutomationElement root, NativeRect rect, double scale)
    {
        foreach (AutomationElement document in Children(root, 8)
            .Where(e => e.Current.ControlType == ControlType.Document))
        foreach (AutomationElement panel in Children(document, 6)
            .Where(e => e.Current.ControlType == ControlType.Custom && !e.Current.IsOffscreen))
        {
            List<AutomationElement> sections = Children(panel, 16);
            // Known footer identifies the tray message surface before any row title is read.
            HashSet<string> footer = [];
            foreach (AutomationElement section in sections)
            {
                var bounds = section.Current.BoundingRectangle;
                if (bounds.Top < rect.Bottom - 42 * scale) continue;
                List<AutomationElement> children = Children(section, 3);
                if (children.Count == 1 && children[0].Current.ControlType == ControlType.Text)
                {
                    string label = children[0].Current.Name;
                    if (label is "忽略全部" or "查看全部") footer.Add(label);
                }
            }
            if (footer.Count != 2) continue;
            List<QqTrayHeader> rows = [];
            foreach (AutomationElement section in sections)
            {
                List<AutomationElement> columns = Children(section, 4);
                if (columns.Count != 2 || columns.Any(e => e.Current.ControlType != ControlType.Custom)) continue;
                List<AutomationElement> fields = Children(columns[1], 5);
                if (fields.Count != 3 || fields[0].Current.ControlType != ControlType.Text ||
                    fields[1].Current.ControlType != ControlType.Custom || fields[2].Current.ControlType != ControlType.Text)
                    return null;
                var titleBounds = fields[0].Current.BoundingRectangle;
                var badgeBounds = fields[2].Current.BoundingRectangle;
                if (fields[0].Current.IsOffscreen || fields[2].Current.IsOffscreen ||
                    titleBounds.Top < rect.Top + 35 * scale ||
                    badgeBounds.Top < titleBounds.Top + 16 * scale ||
                    badgeBounds.Right < rect.Right - 30 * scale ||
                    badgeBounds.Right > rect.Right) return null;
                // fields[1] contains the message preview. Its Name, values and children are NEVER read.
                rows.Add(new QqTrayHeader(fields[0].Current.Name, fields[2].Current.Name));
            }
            return QqTrayDetailsParser.Parse(rows);
        }
        return null;
    }

    private static List<AutomationElement> Children(AutomationElement parent, int maximum)
    {
        List<AutomationElement> children = [];
        TreeWalker walker = TreeWalker.RawViewWalker;
        for (AutomationElement? child = walker.GetFirstChild(parent); child is not null;
            child = walker.GetNextSibling(child))
        {
            if (children.Count == maximum) throw new InvalidOperationException("Unsupported tray layout.");
            children.Add(child);
        }
        return children;
    }

    private delegate bool EnumWindowCallback(nint hwnd, nint parameter);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, StringBuilder name, int count);
}
