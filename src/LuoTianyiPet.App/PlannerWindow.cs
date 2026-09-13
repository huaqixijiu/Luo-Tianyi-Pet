using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LuoTianyiPet.Core;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
namespace LuoTianyiPet.App;
internal sealed partial class PlannerWindow : Window
{
    private readonly ReminderService _service;
    private readonly Grid _shell=new();
    private readonly DockPanel _root=new();
    private readonly StackPanel _header=new();
    private readonly ScrollViewer _body=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    private readonly StackPanel _footer=new();
    private readonly TextBlock _status=Text("",12);
    private readonly DispatcherTimer _clock=new(){Interval=TimeSpan.FromSeconds(1)};
    private readonly List<(TextBlock Text,ReminderItem Item)> _running=[];
    private readonly HashSet<DateTime> _selected=[];
    private readonly HashSet<Guid> _selectedGroups=[];
    private DateTime _date=DateTime.Today;
    private DateTime? _occurrenceDate;
    private bool _alarm,_editing,_batch,_manage,_details=true;
    private static readonly string[] Repeats=["仅一次","每天","每周指定","指定日期","工作日 · 跟随日历","休息日 · 跟随日历"];
    private static readonly DayOfWeek[] Days=[DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday,DayOfWeek.Saturday,DayOfWeek.Sunday];
    public event Action? ReminderSettingsRequested;
    public PlannerWindow(ReminderService service,bool alarm)
    {
        _service=service;_alarm=alarm;PlannerTheme.Apply(this);
        Title="洛天依 · 小天的时光";Width=Math.Min(1200,SystemParameters.WorkArea.Width-24);Height=Math.Min(810,SystemParameters.WorkArea.Height-24);
        WindowStyle=WindowStyle.None;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this,new(){CaptionHeight=0,ResizeBorderThickness=new Thickness(5),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(12)});
        _body.Margin=new Thickness(12,0,12,0);_footer.Margin=new Thickness(20,8,20,10);
        MinWidth=Math.Min(1040,Width);MinHeight=Math.Min(640,Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;
        FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI");FontSize=14;Foreground=PlannerTheme.Ink;Background=new LinearGradientBrush(Color.FromRgb(239,249,255),Colors.White,90);
        Language=System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN");UseLayoutRounding=true;
        DockPanel.SetDock(_header,Dock.Top);_root.Children.Add(_header);DockPanel.SetDock(_footer,Dock.Bottom);_root.Children.Add(_footer);_root.Children.Add(_body);_shell.Children.Add(_root);Content=_shell;
        _service.Changed+=OnChanged;_clock.Tick+=(_,_)=>UpdateRemaining();IsVisibleChanged+=(_,_)=>{if(IsVisible)_clock.Start();else _clock.Stop();};
        Closed+=(_,_)=>{_clock.Stop();_service.Changed-=OnChanged;};Render();
    }
    public void Navigate(bool alarm){_alarm=alarm;_manage=false;_editing=false;Render();Show();Activate();}
    internal void OpenItem(Guid id){var item=_service.Book.Items.FirstOrDefault(i=>i.Id==id);if(item!=null){Navigate(!item.Calendar);Edit(item,item.Calendar);}}
    private void OnChanged(){if(!_editing)Render();}
    private async Task Execute(Action<ReminderBook> action){try{await _service.ChangeAsync(action);_status.Text="已保存到本机";}catch{_status.Text="保存失败，原数据保留。";throw;}}
    private static TextBlock Text(string value,double size=14)=>new(){Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(3),Foreground=size<=12?PlannerTheme.Muted:PlannerTheme.Ink};
    private static StackPanel Row()=>new(){Orientation=Orientation.Horizontal};
    private Button Action(string label,Action action){Button b=new(){Content=label,Margin=new Thickness(3),Padding=new Thickness(14,8,14,8),MinHeight=40,FontSize=14,VerticalAlignment=VerticalAlignment.Center};b.Click+=(_,_)=>action();return b;}
    private Button AsyncAction(string label,Func<Task> action)=>Action(label,async()=>{try{await action();}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){_status.Text=e is ArgumentException?e.Message:"保存失败，原数据保留。";}});
    private static Button Primary(Button b){b.Background=PlannerTheme.PrimaryFill;b.Foreground=Brushes.White;return b;}
    private static Border Card(UIElement child)=>new(){Child=child,Background=Brushes.White,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(9),Margin=new Thickness(2,5,2,5)};
    internal static string Remaining(DateTime at){var t=at-DateTime.Now;return t<=TimeSpan.Zero?"已到时间":t.TotalDays>=1?$"还有{(int)t.TotalDays}天{t.Hours}小时":$"剩余{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";}
    private void UpdateRemaining(){foreach(var pair in _running){var sec=ReminderEngine.Remaining(pair.Item,DateTime.Now);var t=TimeSpan.FromSeconds(sec);pair.Text.Text=$"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";}}
    private void UpdateDateStatus(DateTime day)=>_status.Text=$"{day:yyyy年M月d日 dddd}  {CalendarLabels.FullLunar(day)}";
    private void Render()
    {
        while(_shell.Children.Count>1)_shell.Children.RemoveAt(_shell.Children.Count-1);_root.IsEnabled=true;_header.Children.Clear();_footer.Children.Clear();_running.Clear();
        DockPanel brand=new(){Margin=new Thickness(18,10,14,8)};
        var windows=Row();foreach(var entry in new[]{("—",(Action)(()=>WindowState=WindowState.Minimized)),("□",(Action)(()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized)),("×",(Action)Close)}){var b=Action(entry.Item1,entry.Item2);b.Width=40;b.FontSize=20;b.Background=Brushes.Transparent;b.BorderThickness=new Thickness(0);windows.Children.Add(b);}DockPanel.SetDock(windows,Dock.Right);brand.Children.Add(windows);
        try{var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=new Uri("pack://application:,,,/assets/app/luotianyi-pet.png");bitmap.DecodePixelWidth=128;bitmap.EndInit();var avatar=new System.Windows.Controls.Image{Width=50,Height=50,Margin=new Thickness(0,0,12,0),Source=bitmap};RenderOptions.SetBitmapScalingMode(avatar,BitmapScalingMode.HighQuality);DockPanel.SetDock(avatar,Dock.Left);brand.Children.Add(avatar);}catch{}
        StackPanel identity=new();var brandName=Text("洛天依 · 小天的时光",19);brandName.FontWeight=FontWeights.SemiBold;identity.Children.Add(brandName);identity.Children.Add(Text("在每一个平凡的日子里，与你相遇。",13));brand.Children.Add(identity);brand.MouseLeftButtonDown+=(_,e)=>{if(e.ClickCount==2)WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else if(e.LeftButton==System.Windows.Input.MouseButtonState.Pressed)DragMove();};_header.Children.Add(brand);
        DockPanel tabs=new(){Margin=new Thickness(16,0,16,0)};var settings=Action("⚙",()=>ShowPlannerSettings());settings.FontSize=21;settings.BorderThickness=new Thickness(0);settings.Background=Brushes.Transparent;settings.ToolTip="通知与闹钟设置";DockPanel.SetDock(settings,Dock.Right);tabs.Children.Add(settings);
        StackPanel pages=Row();
        foreach(var entry in new[]{("日历",false,"▦"),("闹钟",true,"◷")})
        {
            var tab=Action(entry.Item1,()=>Navigate(entry.Item2));tab.Width=118;tab.Height=45;tab.Background=Brushes.Transparent;tab.BorderThickness=new Thickness(0);tab.FontSize=18;tab.FontWeight=FontWeights.SemiBold;
            StackPanel label=Row();label.Children.Add(PlannerTheme.Icon(entry.Item2?"clock":"calendar",23,_alarm==entry.Item2?PlannerTheme.Accent:PlannerTheme.Ink));label.Children.Add(Text(entry.Item1,18));tab.Content=label;
            if(_alarm==entry.Item2)foreach(TextBlock part in label.Children.OfType<TextBlock>())part.Foreground=PlannerTheme.Accent;
            pages.Children.Add(new Border{Child=tab,BorderThickness=new Thickness(0,0,0,2),BorderBrush=_alarm==entry.Item2?PlannerTheme.Accent:Brushes.Transparent,Margin=new Thickness(0,0,12,0)});
        }
        tabs.Children.Add(pages);_header.Children.Add(new Border{Child=tabs,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(0,1,0,1),Margin=new Thickness(0,0,0,12)});
        _body.VerticalScrollBarVisibility=_alarm||_manage?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
        if(_manage)RenderGroups();else if(_alarm)RenderAlarms();else RenderCalendar();
        if(!_alarm)_footer.Children.Add(_status);UpdateDateStatus(_date);UpdateRemaining();
    }
    private void RenderCalendar()
    {
        bool week=_service.Book.WeekView;
        Grid layout=new();layout.ColumnDefinitions.Add(new(){Width=new GridLength(7,GridUnitType.Star)});if(!week&&_details)layout.ColumnDefinitions.Add(new(){Width=new GridLength(3,GridUnitType.Star)});
        DockPanel main=new(){Margin=new Thickness(0,0,!week&&_details?10:0,0)};
        DockPanel nav=new(){Margin=new Thickness(0,0,0,10)};StackPanel left=Row();left.Children.Add(Action("‹",()=>MoveDate(week?-7:-1,week)));left.Children.Add(Text(week?$"{WeekStart(_date):M月d日} — {WeekStart(_date).AddDays(6):M月d日}":_date.ToString("yyyy年M月"),18));left.Children.Add(Action("›",()=>MoveDate(week?7:1,week)));left.Children.Add(Action("今天",()=>{_date=DateTime.Today;_details=true;Render();}));
        var rest=Action(_batch?"完成班休":"设置班休",()=>{_batch=!_batch;_selected.Clear();Render();});rest.Name="SetWorkdays";left.Children.Add(rest);
        StackPanel right=Row();right.Children.Add(week?Action("月",()=>SetView(false)):Primary(Action("月",()=>SetView(false))));right.Children.Add(week?Primary(Action("周",()=>SetView(true))):Action("周",()=>SetView(true)));
        var add=Primary(Action("＋ 新增事项",()=>{_occurrenceDate=null;Edit(null,true);}));right.Children.Add(add);
        if(Width<1000&&!week&&_details){DockPanel.SetDock(left,Dock.Top);nav.Children.Add(left);right.HorizontalAlignment=System.Windows.HorizontalAlignment.Right;nav.Children.Add(right);}else{DockPanel.SetDock(right,Dock.Right);nav.Children.Add(right);nav.Children.Add(left);}DockPanel.SetDock(nav,Dock.Top);main.Children.Add(nav);
        if(_batch){var controls=RestControls();DockPanel.SetDock(controls,Dock.Bottom);main.Children.Add(controls);}
        Grid grid=new();for(int c=0;c<7;c++)grid.ColumnDefinitions.Add(new());grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        for(int c=0;c<7;c++){var t=Text("周"+"一二三四五六日"[c],14);t.TextAlignment=TextAlignment.Center;t.Margin=new Thickness(0,9,0,9);Grid.SetColumn(t,c);grid.Children.Add(t);}
        DateTime first=week?WeekStart(_date):WeekStart(new DateTime(_date.Year,_date.Month,1));int count=week?7:((int)(new DateTime(_date.Year,_date.Month,DateTime.DaysInMonth(_date.Year,_date.Month))-first).TotalDays/7+1)*7;
        for(int r=0;r<count/7;r++)grid.RowDefinitions.Add(new());
        for(int n=0;n<count;n++)
        {
            DateTime day=first.AddDays(n);bool isRest=_service.Book.IsRest(day);var items=DayItems(day).ToList();DockPanel cell=new();
            StackPanel dates=new();DockPanel heading=new();var number=Text(week?day.ToString("M月d日"):day.Month==_date.Month?day.Day.ToString():day.ToString("M/d"),week?16:15);number.FontWeight=FontWeights.SemiBold;
            if(day==DateTime.Today){number.Foreground=PlannerTheme.Accent;number.TextDecorations=TextDecorations.Underline;}
            var badge=Text(isRest?"休":"班",11);badge.Foreground=isRest?Brushes.Crimson:Brushes.RoyalBlue;badge.TextAlignment=TextAlignment.Center;Border badgeBox=new(){Child=badge,Width=22,Height=22,Background=isRest?Brushes.MistyRose:Brushes.AliceBlue,CornerRadius=new CornerRadius(4),Margin=new Thickness(1)};DockPanel.SetDock(badgeBox,Dock.Right);
            if(week||isRest||_service.Book.RestOverrides.ContainsKey(day.ToString("yyyy-MM-dd")))heading.Children.Add(badgeBox);heading.Children.Add(number);dates.Children.Add(heading);
            var lunar=Text(CalendarLabels.LunarDay(day),13);lunar.Foreground=Brushes.SlateGray;dates.Children.Add(lunar);var holiday=CalendarLabels.Get(day);if(holiday.Length>0){var h=Text(holiday,12);h.Foreground=Brushes.Crimson;dates.Children.Add(h);}
            if(!week&&items.Count>0){var dots=Text(new string('●',Math.Min(3,items.Count)),10);dots.Foreground=PlannerTheme.Accent;dates.Children.Add(dots);}
            number.Margin=new Thickness(2,0,2,0);lunar.Margin=new Thickness(2,0,2,0);
            foreach(var label in dates.Children.OfType<TextBlock>())label.Margin=new Thickness(2,0,2,0);
            Button dateButton=Action("",()=>{if(_batch){if(!_selected.Add(day))_selected.Remove(day);}else{_details=!(_details&&_date==day);_date=day;}Render();});dateButton.Name="Day"+day.ToString("yyyyMMdd");dateButton.Content=dates;dateButton.Padding=new Thickness(0);dateButton.Margin=new Thickness(0);dateButton.BorderThickness=new Thickness(0);dateButton.Background=Brushes.Transparent;dateButton.HorizontalContentAlignment=System.Windows.HorizontalAlignment.Stretch;dateButton.VerticalAlignment=VerticalAlignment.Stretch;dateButton.VerticalContentAlignment=VerticalAlignment.Top;
            dateButton.IsEnabled=day>=ReminderSchedule.MinimumDate&&day<=ReminderSchedule.MaximumDate;
            dateButton.MouseDoubleClick+=(_,_)=>{if(!_batch){_date=day;_occurrenceDate=null;Edit(null,true);}};
            if(week){DockPanel.SetDock(dateButton,Dock.Top);cell.Children.Add(dateButton);StackPanel cards=new();foreach(var item in items)cards.Children.Add(EventCard(item,day,true));cell.Children.Add(new ScrollViewer{Content=cards,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});}else cell.Children.Add(dateButton);
            Border border=new(){Child=cell,Padding=new Thickness(8),CornerRadius=new CornerRadius(3),BorderBrush=_selected.Contains(day)||!_batch&&day==_date?PlannerTheme.Accent:PlannerTheme.Line,BorderThickness=new Thickness(1),Background=_selected.Contains(day)||!_batch&&day==_date?PlannerTheme.Soft:Brushes.White};Grid.SetColumn(border,n%7);Grid.SetRow(border,n/7+1);grid.Children.Add(border);
        }
        main.Children.Add(grid);layout.Children.Add(main);
        if(!week&&_details){var detail=DayDetails();Grid.SetColumn(detail,1);layout.Children.Add(detail);}
        _body.Content=layout;
        var foot=Row();foot.Children.Add(Text("● 有事项",11));var restLegend=Text("休 休息日",11);restLegend.Foreground=Brushes.Crimson;foot.Children.Add(restLegend);var workLegend=Text("班 工作日",11);workLegend.Foreground=Brushes.RoyalBlue;foot.Children.Add(workLegend);_footer.Children.Add(foot);
    }
    private IEnumerable<ReminderItem> DayItems(DateTime day)=>_service.Book.Items.Where(i=>i.Calendar&&ReminderSchedule.OccursOn(i,_service.Book,day)).OrderBy(i=>i.HasTime?1:0).ThenBy(i=>i.Start.TimeOfDay);
    private UIElement EventCard(ReminderItem item,DateTime day,bool week)
    {
        DockPanel content=new();Button more=Action("⋯",()=>ItemMenu(item,day));more.Width=20;more.Padding=new Thickness(3);more.MinHeight=24;more.BorderThickness=new Thickness(0);DockPanel.SetDock(more,Dock.Right);if(!week)content.Children.Add(more);
        StackPanel info=new();var title=Text((item.HasTime?item.Start.ToString("HH:mm")+(week?"\n":"  "):"")+ReminderEngine.Label(item),14);title.FontWeight=FontWeights.SemiBold;
        if(item.Enabled&&item.HasTime)title.Inlines.InsertBefore(title.Inlines.FirstInline,new System.Windows.Documents.InlineUIContainer(PlannerTheme.Bell()));
        info.Children.Add(title);
        if(!string.IsNullOrWhiteSpace(item.Notes)){var notes=Text(item.Notes,13);notes.Foreground=Brushes.SlateGray;notes.MaxHeight=week?65:42;notes.TextTrimming=TextTrimming.CharacterEllipsis;info.Children.Add(notes);}content.Children.Add(info);
        var button=Action("",()=>{_occurrenceDate=day;Edit(item,true);});button.Content=new Border{Child=content,BorderBrush=PlannerTheme.Accent,BorderThickness=new Thickness(3,0,0,0),Padding=new Thickness(7,3,0,3)};button.HorizontalContentAlignment=System.Windows.HorizontalAlignment.Stretch;button.Padding=new Thickness(8);button.Margin=new Thickness(0,6,0,6);button.Background=week?PlannerTheme.Soft:Brushes.White;return button;
    }
    private UIElement DayDetails()
    {
        DockPanel panel=new(){Margin=new Thickness(14,0,2,0)};DockPanel title=new();var close=Action("×",()=>{_details=false;Render();});DockPanel.SetDock(close,Dock.Right);title.Children.Add(close);title.Children.Add(Text(_date.ToString("M月d日 dddd"),18));DockPanel.SetDock(title,Dock.Top);panel.Children.Add(title);
        var lunar=Text(CalendarLabels.FullLunar(_date)+" "+CalendarLabels.Get(_date),12);lunar.Foreground=Brushes.SlateGray;DockPanel.SetDock(lunar,Dock.Top);panel.Children.Add(lunar);
        var add=Action("＋ 添加当天事项",()=>{_occurrenceDate=null;Edit(null,true);});add.Name="AddSelectedDay";DockPanel.SetDock(add,Dock.Bottom);panel.Children.Add(add);
        StackPanel list=new();foreach(var item in DayItems(_date))list.Children.Add(EventCard(item,_date,false));if(list.Children.Count==0)list.Children.Add(Text("当天暂无事项",14));panel.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});return panel;
    }
    private void ItemMenu(ReminderItem item,DateTime day)
    {
        System.Windows.Controls.ContextMenu menu=new();foreach(var label in new[]{"编辑事项","仅删除当天","删除整组…","批量管理…"}){var entry=new System.Windows.Controls.MenuItem{Header=label};entry.Click+=(_,_)=>{if(label=="编辑事项"){_occurrenceDate=day;Edit(item,true);}else if(label=="批量管理…"){_manage=true;Render();}else ConfirmDelete([item.Id],label=="仅删除当天"?day:null);};menu.Items.Add(entry);}menu.IsOpen=true;
    }
    private void SetView(bool week)=>_=Execute(b=>b.WeekView=week);
    private static DateTime WeekStart(DateTime date)=>date.Date.AddDays(-((int)date.DayOfWeek+6)%7);
    private void MoveDate(int offset,bool week){DateTime next=week?_date.AddDays(offset):_date.AddMonths(offset);if(next>=ReminderSchedule.MinimumDate&&next<=ReminderSchedule.MaximumDate)_date=next;Render();}
    private void EditWorkdays(){_batch=true;_alarm=false;Render();}
    private UIElement RestControls()
    {
        StackPanel panel=new();WrapPanel days=new();days.Children.Add(Text("每周休息"));foreach(var d in Days){var btn=AsyncAction("周"+"日一二三四五六"[(int)d],()=>Execute(b=>{var selected=b.RestWeekdays.ToList();if(!selected.Remove(d))selected.Add(d);ReminderSchedule.SetRestWeekdays(b,selected,DateTime.Now);}));if(_service.Book.RestWeekdays.Contains(d))Primary(btn);days.Children.Add(btn);}panel.Children.Add(days);
        WrapPanel actions=new();actions.Children.Add(Text($"已选{_selected.Count}天"));actions.Children.Add(AsyncAction("设为工作日",()=>SetRest(false)));actions.Children.Add(AsyncAction("设为休息日",()=>SetRest(true)));actions.Children.Add(AsyncAction("恢复默认",()=>SetRest(null)));actions.Children.Add(AsyncAction("周六日休息",()=>Execute(b=>ReminderSchedule.SetRestWeekdays(b,[DayOfWeek.Saturday,DayOfWeek.Sunday],DateTime.Now))));panel.Children.Add(actions);return panel;
    }
    private Task SetRest(bool? rest)=>Execute(b=>{foreach(var d in _selected)ReminderSchedule.SetRestOverride(b,d,rest,DateTime.Now);});
    private void RenderAlarms()
    {
        DockPanel header=new(){Margin=new Thickness(20,0,20,10)};var add=Primary(Action("＋ 新建提醒",()=>Edit(null,false)));add.Name="NewAlarm";DockPanel.SetDock(add,Dock.Right);header.Children.Add(add);header.Children.Add(Text("闹钟",22));_header.Children.Add(header);
        StackPanel list=new();
        var groups=new[]{("正在倒计时",_service.Book.Items.Where(i=>i.Relative && (i.PausedSeconds!=null || i.Enabled && (i.Start>DateTime.Now || _service.Book.Occurrences.Any(o=>o.RuleId==i.Id&&o.Phase is ReminderPhase.Due or ReminderPhase.DueSnoozed))))),("我的闹钟",_service.Book.Items.Where(i=>!i.Relative&&!i.Calendar)),("日历提醒",_service.Book.Items.Where(i=>i.Calendar&&i.ReminderCreated==true))};
        foreach(var group in groups){var items=group.Item2.OrderBy(i=>ReminderSchedule.Next(i,_service.Book,DateTime.Now)??DateTime.MaxValue).ToList();if(items.Count==0)continue;StackPanel section=new();var heading=Text($"{group.Item1} ({items.Count})",18);heading.FontWeight=FontWeights.SemiBold;heading.Margin=new Thickness(8,6,8,12);section.Children.Add(heading);foreach(var i in items)section.Children.Add(AlarmRow(i));var card=Card(section);card.Padding=new Thickness(12);if(group.Item1=="正在倒计时")card.Background=PlannerTheme.Soft;list.Children.Add(card);}
        if(list.Children.Count==0)list.Children.Add(Text("暂无提醒，点击右上角「新建提醒」开始。",16));_body.Content=list;
    }
    private UIElement AlarmRow(ReminderItem item)
    {
        Grid row=new(){Margin=new Thickness(10,10,10,10)};row.ColumnDefinitions.Add(new(){Width=new GridLength(170)});row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=new GridLength(240)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        TextBlock clock=Text(item.Start.ToString("HH:mm"),30);clock.FontFamily=new System.Windows.Media.FontFamily("Segoe UI");clock.FontWeight=FontWeights.SemiBold;row.Children.Add(clock);if(item.Relative){_running.Add((clock,item));clock.Foreground=PlannerTheme.Accent;}
        StackPanel details=new();var name=Text(ReminderEngine.Label(item),16);name.FontWeight=FontWeights.SemiBold;details.Children.Add(name);details.Children.Add(Text(item.Relative?$"共 {TimeSpan.FromSeconds(item.DurationSeconds).TotalMinutes:0.##} 分钟" : item.Calendar?"来自日历 · "+item.Start.ToString("M月d日"):item.Repeat==ReminderRepeat.Weekly?"每周"+string.Join("、",item.Weekdays.Select(d=>"日一二三四五六"[(int)d])):Repeats[(int)item.Repeat],12));
        var next=ReminderSchedule.Next(item,_service.Book,DateTime.Now);var nextText=Text(item.PausedSeconds!=null?"已暂停":!item.Enabled?"已关闭":next is DateTime at?(item.Relative?"结束：":"下一次：")+at.ToString("M月d日 HH:mm"):"已到时间",13);nextText.Foreground=PlannerTheme.Muted;Grid.SetColumn(nextText,2);row.Children.Add(nextText);Grid.SetColumn(details,1);row.Children.Add(details);
        StackPanel controls=Row();if(item.Relative){controls.Children.Add(AsyncAction(item.PausedSeconds==null?"暂停":"继续",()=>Execute(b=>{if(item.PausedSeconds==null)ReminderEngine.Pause(b,item.Id,DateTime.Now);else ReminderEngine.Resume(b,item.Id,DateTime.Now);})));controls.Children.Add(AsyncAction("取消",()=>Execute(b=>b.Items.RemoveAll(i=>i.Id==item.Id))));}
        else {var enabled=new CheckBox{IsChecked=item.Enabled,Style=(Style)FindResource("PlannerSwitch"),Margin=new Thickness(8),VerticalAlignment=VerticalAlignment.Center};enabled.Click+=async(_,_)=>{try{await Execute(b=>ReminderEngine.SetEnabled(b,item.Id,enabled.IsChecked==true,DateTime.Now));}catch{}};controls.Children.Add(enabled);controls.Children.Add(Action("⋯",()=>{_occurrenceDate=null;Edit(item,item.Calendar);}));}
        Grid.SetColumn(controls,3);row.Children.Add(controls);return new Border{Child=row,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(0,1,0,0),Margin=new Thickness(4,0,4,0)};
    }
    private void ShowPlannerSettings(){if(ReminderSettingsRequested!=null)ReminderSettingsRequested();else {var settings=new SettingsWindow(new(),new(),new(),new(),new(),false,null,_service.Book.Preferences);settings.NavigateNotifications();if(settings.ShowDialog()==true)_=Execute(b=>b.Preferences=settings.SelectedReminderPreferences);}}
}
