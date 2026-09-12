using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using LuoTianyiPet.Core;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Pen = System.Windows.Media.Pen;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace LuoTianyiPet.App;

public sealed class MessageInboxWindow : Window
{
    private readonly MessageInbox _inbox;
    private readonly Canvas _canvas = new();
    private readonly StackPanel _icons = new();
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _scroll;
    private readonly Border _panel;
    private readonly Polygon _fold = new() { IsHitTestVisible = false };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly Dictionary<MessageProvider, FrameworkElement> _iconElements = [];
    private readonly Dictionary<string, (Border Root, TextBlock Name, TextBlock Preview, TextBlock Count)> _rowElements = [];
    private MessageProvider? _renderedProvider;
    private bool _contextOpen, _leftDown;
    private Rect _iconBounds, _panelBounds, _bridge;
    private MessageRailLayout? _layout;
    public MessageInboxInteraction Interaction { get; } = new();
    public event Action? PositionRequested;
    public event Action? Changed;
    public bool PointerInside { get; private set; }
    public bool AutomationMode { get; set; }
    public Rect PanelRectangle => _panelBounds;
    public Point IconCenter(MessageProvider provider) => new(_iconBounds.Left+14,
        _iconBounds.Top+(provider==MessageProvider.Qq && _inbox.Total(MessageProvider.WeChat)>0 ? 30 : 0)+18);
    public IReadOnlyList<string> VisibleOrder => _rows.Children.OfType<Border>().Select(row => (string)row.Tag).ToArray();
    public double ScrollOffset => _scroll.VerticalOffset;

    public MessageInboxWindow(MessageInbox inbox)
    {
        _inbox = inbox;
        Title = "QQ／微信待处理提醒"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false;
        Content = _canvas;
        _scroll = new() { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false, Focusable = false };
        // Narrow scrollbars leave the 192-DIP panel's two text lines readable.
        var barStyle = new Style(typeof(System.Windows.Controls.Primitives.ScrollBar));
        barStyle.Setters.Add(new Setter(WidthProperty, 7.0));
        _scroll.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), barStyle);
        _panel = new() { Child = _scroll, CornerRadius = new(6), BorderThickness = new(1), Padding = new(1),
            Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 1, Opacity = .12 } };
        _canvas.Children.Add(_panel); _canvas.Children.Add(_fold); _canvas.Children.Add(_icons);
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x08000000 | 0x80);
            HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        };
        _timer.Tick += (_, _) => Tick();
        Closed += (_, _) => _timer.Stop();
        Refresh();
    }

    public void Refresh()
    {
        var providers = new[] { MessageProvider.WeChat, MessageProvider.Qq }.Where(p => _inbox.Total(p) > 0).ToArray();
        if (providers.Length == 0) { Interaction.Close(); Hide(); _timer.Stop(); }
        if (Interaction.OpenProvider is MessageProvider open && _inbox.Total(open) == 0) Interaction.Close();
        foreach (var stale in _iconElements.Keys.Except(providers).ToArray())
        { _icons.Children.Remove(_iconElements[stale]); _iconElements.Remove(stale); }
        for (int i = 0; i < providers.Length; i++)
        {
            var provider = providers[i];
            if (!_iconElements.TryGetValue(provider, out var element))
            {
                element = MakeIcon(provider); _iconElements[provider] = element;
                _icons.Children.Insert(i, element);
            }
            var badge = (TextBlock)((Border)((Grid)element).Children[1]).Child;
            badge.Text = MessageInbox.Badge(_inbox.Total(provider));
            System.Windows.Automation.AutomationProperties.SetName(element,
                $"{MessageProviderMatcher.GetDisplayName(provider)}，{_inbox.Total(provider)}次待处理提醒");
        }
        _panel.Visibility = _fold.Visibility = Interaction.OpenProvider is null ? Visibility.Collapsed : Visibility.Visible;
        if (Interaction.OpenProvider is MessageProvider selected) RenderRows(selected);
        else { _renderedProvider = null; _rowElements.Clear(); _rows.Children.Clear(); }
        PositionRequested?.Invoke();
    }

    private FrameworkElement MakeIcon(MessageProvider provider)
    {
        Grid element = new() { Width = 38, Height = 30, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        var art = new Image { Source = ProviderDrawing(provider), Width = 24, Height = 24,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(2,0,0,0) };
        element.Children.Add(art);
        Border badge = new() { Background = new SolidColorBrush(Color.FromRgb(0xF4,0x55,0x62)), CornerRadius = new(7),
            Padding = new(3,0,3,0), MinWidth = 13, Height = 13, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
            Child = new TextBlock { Foreground = Brushes.White, FontSize = 9, FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center } };
        element.Children.Add(badge);
        element.MouseLeftButtonUp += (_, e) => { Interaction.Click(provider); Refresh(); e.Handled = true; };
        element.ContextMenu = Menu("清空该应用提醒", () => { _inbox.Clear(provider, DateTimeOffset.Now); Refresh(); Changed?.Invoke(); });
        return element;
    }

    private ContextMenu Menu(string label, Action action)
    {
        ContextMenu menu = new(); MenuItem item = new() { Header = label }; menu.Items.Add(item);
        item.Click += (_, _) => action(); menu.Opened += (_, _) => _contextOpen = true;
        menu.Closed += (_, _) => _contextOpen = false; return menu;
    }

    private void RenderRows(MessageProvider provider)
    {
        var items = _inbox.Rows(provider);
        Color accent = provider == MessageProvider.WeChat ? Color.FromRgb(0x19,0xB9,0x70) : Color.FromRgb(0x32,0xA8,0xED);
        var ink = new SolidColorBrush(accent);
        _panel.Background = new SolidColorBrush(provider == MessageProvider.WeChat ? Color.FromRgb(0xF2,0xFF,0xF7) : Color.FromRgb(0xF2,0xFA,0xFF));
        _panel.BorderBrush = ink; _fold.Fill = ink;
        if (_renderedProvider != provider) { _rows.Children.Clear(); _rowElements.Clear(); _scroll.ScrollToTop(); _renderedProvider = provider; }
        // Keep established row order while open. New conversations append until the next open.
        var keys = new HashSet<string>(items.Select(row => row.Key));
        double offset = _scroll.VerticalOffset;
        string? firstVisible = VisibleOrder.ElementAtOrDefault((int)(offset / 46));
        foreach (var key in _rowElements.Keys.Where(key => !keys.Contains(key)).ToArray())
        { _rows.Children.Remove(_rowElements[key].Root); _rowElements.Remove(key); }
        foreach (var item in items)
        {
            if (!_rowElements.TryGetValue(item.Key, out var controls))
            {
                Grid grid = new() { Margin = new(7,4,7,4) };
                grid.RowDefinitions.Add(new() { Height = new(19) }); grid.RowDefinitions.Add(new() { Height = new(18) });
                grid.ColumnDefinitions.Add(new() { Width = new(1,GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                TextBlock name = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x24,0x3D,0x50)), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0,0,4,0) };
                TextBlock preview = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x48,0x60,0x6E)), TextTrimming = TextTrimming.CharacterEllipsis };
                TextBlock count = new() { FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = ink, TextAlignment = TextAlignment.Center };
                Border number = new() { Child = count, CornerRadius = new(7), Padding = new(4,0,4,0), MinWidth = 20, Height = 16,
                    Background = new SolidColorBrush(Color.FromArgb(24,accent.R,accent.G,accent.B)) };
                Grid.SetColumn(number,1); Grid.SetRow(preview,1); Grid.SetColumnSpan(preview,2);
                grid.Children.Add(name); grid.Children.Add(number); grid.Children.Add(preview);
                Border row = new() { Height = 46, Child = grid, Background = Brushes.Transparent, Tag = item.Key,
                    BorderThickness = new(0,0,0,1), BorderBrush = new SolidColorBrush(Color.FromArgb(32,accent.R,accent.G,accent.B)) };
                row.ContextMenu = Menu("忽略该会话提醒", () => { _inbox.Ignore(item.Key); Refresh(); Changed?.Invoke(); });
                _rows.Children.Add(row); controls = (row,name,preview,count); _rowElements[item.Key] = controls;
            }
            controls.Name.Text = item.Name; controls.Preview.Text = item.Preview; controls.Count.Text = MessageInbox.Badge(item.Count);
        }
        int anchorIndex = firstVisible is null ? -1 : VisibleOrder.ToList().IndexOf(firstVisible);
        if (anchorIndex >= 0) _scroll.ScrollToVerticalOffset(anchorIndex * 46 + offset % 46);
    }

    public void SetLayout(MessageRailLayout layout, double scale)
    {
        _layout = layout;
        bool expanded = Interaction.OpenProvider is not null;
        var icons = layout.Icons; var panel = layout.Panel;
        double x = expanded ? Math.Min(icons.Left,panel.Left) - 5 * scale : icons.Left - 2 * scale;
        double y = expanded ? Math.Min(icons.Top,panel.Top) - 5 * scale : icons.Top - 2 * scale;
        double right = expanded ? Math.Max(icons.Right,panel.Right) + 5 * scale : icons.Right + 2 * scale;
        double bottom = expanded ? Math.Max(icons.Bottom,panel.Bottom) + 5 * scale : icons.Bottom + 2 * scale;
        Width = (right-x)/scale; Height = (bottom-y)/scale;
        _iconBounds = new((icons.Left-x)/scale,(icons.Top-y)/scale,icons.Width/scale,icons.Height/scale);
        _panelBounds = new((panel.Left-x)/scale,(panel.Top-y)/scale,panel.Width/scale,panel.Height/scale);
        Canvas.SetLeft(_icons,_iconBounds.Left); Canvas.SetTop(_icons,_iconBounds.Top);
        Canvas.SetLeft(_panel,_panelBounds.Left); Canvas.SetTop(_panel,_panelBounds.Top);
        _panel.Width = _panelBounds.Width; _panel.Height = _panelBounds.Height;
        double bridgeLeft = layout.Side == MessageRailSide.Left ? _panelBounds.Right : _iconBounds.Right;
        double bridgeRight = layout.Side == MessageRailSide.Left ? _iconBounds.Left : _panelBounds.Left;
        _bridge = new Rect(bridgeLeft,_iconBounds.Top+ActiveIconIndex()*30,Math.Max(0,bridgeRight-bridgeLeft),30);
        double foldX = layout.Side == MessageRailSide.Left ? _panelBounds.Right - 1 : _panelBounds.Left - 4;
        double foldY = Numeric.Clamp(_iconBounds.Top + ActiveIconIndex() * 30 + 10, _panelBounds.Top+5, _panelBounds.Bottom-10);
        _fold.Points = layout.Side == MessageRailSide.Left
            ? new(new[] {new Point(foldX,foldY),new Point(foldX+5,foldY+3),new Point(foldX,foldY+6)})
            : new(new[] {new Point(foldX+5,foldY),new Point(foldX,foldY+3),new Point(foldX+5,foldY+6)});
        if (!IsVisible) { Opacity = 0; Show(); }
        SetWindowPos(new WindowInteropHelper(this).Handle,0,(int)Math.Round(x),(int)Math.Round(y),
            (int)Math.Ceiling(right-x),(int)Math.Ceiling(bottom-y),0x14);
        Opacity = 1; _timer.Start();
    }

    public int ActiveIconIndex() => Interaction.OpenProvider == MessageProvider.Qq && _inbox.Total(MessageProvider.WeChat) > 0 ? 1 : 0;
    public void Suspend() { Interaction.Close(); Hide(); _timer.Stop(); PointerInside = false; }
    public void ScrollTo(double offset) => _scroll.ScrollToVerticalOffset(offset);
    public void ExposeAutomationWindow()
    {
        ShowInTaskbar = true;
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowLong(handle,-20,(GetWindowLong(handle,-20)&~0x80)|0x40000);
    }
    internal void QaClickIcon(MessageProvider provider) => _iconElements[provider].RaiseEvent(
        new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left) {RoutedEvent=MouseLeftButtonUpEvent});
    internal void QaIgnoreConversation(string key) => ((MenuItem)_rowElements[key].Root.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    internal void QaClearApplication(MessageProvider provider) => ((MenuItem)_iconElements[provider].ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    internal bool QaNativeHitTest(Point point)
    {
        Point screen=PointToScreen(point); long packed=((long)(ushort)(short)screen.Y<<16)|(ushort)(short)screen.X;
        return SendMessage(new WindowInteropHelper(this).Handle,0x84,0,(nint)packed)==1;
    }
    public bool IsInteractive(Point point) => _iconBounds.Contains(point) ||
        (Interaction.OpenProvider is not null && (_panelBounds.Contains(point) || _bridge.Contains(point)));

    private void Tick()
    {
        if (AutomationMode || !IsVisible || _layout is null || !GetCursorPos(out var cursor)) return;
        Point point = PointFromScreen(new(cursor.X,cursor.Y));
        ProcessPointer(point,(GetAsyncKeyState(1) & 0x8000) != 0,DateTimeOffset.Now);
    }
    public void ProcessPointer(Point point, bool down, DateTimeOffset now)
    {
        PointerInside = IsInteractive(point);
        var before = Interaction.OpenProvider;
        MessageProvider? icon = null;
        if (_iconBounds.Contains(point))
        {
            int index = (int)((point.Y-_iconBounds.Top)/30);
            var providers = new[] {MessageProvider.WeChat,MessageProvider.Qq}.Where(p=>_inbox.Total(p)>0).ToArray();
            if (index >= 0 && index < providers.Length) icon = providers[index];
        }
        if (down && !_leftDown && !PointerInside && !_contextOpen) Interaction.OutsideClick();
        _leftDown = down;
        if (!_contextOpen) Interaction.Move(icon, Interaction.OpenProvider is not null && (_panelBounds.Contains(point) || _bridge.Contains(point)), now);
        if (before != Interaction.OpenProvider) Refresh();
        PositionRequested?.Invoke();
    }
    private nint WndProc(nint hwnd,int msg,nint wp,nint lp,ref bool handled)
    {
        if (msg == 0x21) { handled = true; return 3; } // MA_NOACTIVATE
        if (msg == 0x84)
        {
            Point point = PointFromScreen(new((short)((long)lp & 0xffff),(short)(((long)lp >> 16)&0xffff)));
            if (!IsInteractive(point)) { handled = true; return -1; }
        }
        return 0;
    }

    private static ImageSource ProviderDrawing(MessageProvider provider)
    {
        DrawingGroup group = new();
        using (var d = group.Open())
        {
            if (provider == MessageProvider.WeChat)
            {
                var green = new SolidColorBrush(Color.FromRgb(0x12,0xC4,0x61));
                d.DrawRoundedRectangle(green,null,new(0,0,24,24),5,5);
                d.DrawEllipse(Brushes.White,null,new(9,10),6.3,5.1);
                d.DrawGeometry(Brushes.White,null,Geometry.Parse("M 5,13 L 4,17 L 8,15 Z"));
                d.DrawEllipse(green,null,new(6.8,9),.8,.8); d.DrawEllipse(green,null,new(11,9),.8,.8);
                d.DrawEllipse(Brushes.White,new Pen(green,.8),new(16,15.2),5.5,4.4);
                d.DrawGeometry(Brushes.White,null,Geometry.Parse("M 17,18 L 21,21 L 20,17 Z"));
                d.DrawEllipse(green,null,new(14.3,14.4),.65,.65); d.DrawEllipse(green,null,new(18,14.4),.65,.65);
            }
            else
            {
                d.DrawEllipse(Brushes.Orange,null,new(7,22),4,1.7); d.DrawEllipse(Brushes.Orange,null,new(17,22),4,1.7);
                d.DrawEllipse(Brushes.Black,null,new(12,13),8,10); d.DrawEllipse(Brushes.Black,null,new(12,6),6,6);
                d.DrawEllipse(Brushes.Black,null,new(4,15),2,5); d.DrawEllipse(Brushes.Black,null,new(20,15),2,5);
                d.DrawEllipse(Brushes.White,null,new(12,16),5.8,6);
                d.DrawEllipse(Brushes.White,null,new(9.7,5.8),1.7,2.5); d.DrawEllipse(Brushes.White,null,new(14.3,5.8),1.7,2.5);
                d.DrawEllipse(Brushes.Black,null,new(10,6),.6,1); d.DrawEllipse(Brushes.Black,null,new(14,6),.6,1);
                d.DrawEllipse(Brushes.Orange,null,new(12,9),3.8,1.5);
                d.DrawRoundedRectangle(Brushes.Red,null,new(5,10.5,14,2.8),1,1);
                d.DrawRectangle(Brushes.Red,null,new(15,12,3,5));
            }
        }
        group.Freeze(); var image = new DrawingImage(group); image.Freeze(); return image;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X,Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd,int message,nint wp,nint lp);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] private static extern int GetWindowLong(nint hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")] private static extern int SetWindowLong(nint hwnd,int index,int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
}
