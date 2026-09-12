using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Calendar = System.Windows.Controls.Calendar;

namespace LuoTianyiPet.App;

internal sealed partial class PlannerWindow : Window
{
    private readonly ReminderService _service;
    private readonly Grid _shell = new();
    private DateTime? _occurrenceDate;
    private bool _manage;
    private readonly HashSet<Guid> _selectedGroups = [];
    private readonly DockPanel _root = new() { Margin = new Thickness(22, 14, 22, 12) };
    private readonly StackPanel _header = new();
    private readonly ScrollViewer _body = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _status = Text("", 12);
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<(TextBlock Text, DateTime At)> _remaining = [];
    private readonly HashSet<DateTime> _selected = [];
    private DateTime _date = DateTime.Today;
    private bool _alarm, _editing, _batch;
    private static readonly string[] Repeats = ["单次", "每天", "每周几", "指定多个日期", "工作日", "休息日"];
    private static readonly DayOfWeek[] Days = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
    public PlannerWindow(ReminderService service, bool alarm)
    {
        PlannerTheme.Apply(this);
        _service = service;
        _alarm = alarm;
        Title = "天依 · 日历与闹钟";
        Language = System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN");
        Width = Math.Min(alarm ? 680 : 960, SystemParameters.WorkArea.Width - 24); Height = Math.Min(alarm ? 760 : 800, SystemParameters.WorkArea.Height - 24);
        MinWidth = Math.Min(680, Width); MinHeight = Math.Min(470, Height);
        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(247, 251, 251));
        Foreground = Brushes.DarkSlateGray;
        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"); FontSize = 15;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        DockPanel.SetDock(_header, Dock.Top); _root.Children.Add(_header);
        DockPanel footer = new() { Margin = new Thickness(0, 6, 0, 0) };
        TextBlock quote = Text("平凡的日子里，也有可爱的期待。", 11); quote.Foreground = Brushes.SlateGray; DockPanel.SetDock(quote, Dock.Right); footer.Children.Add(quote); footer.Children.Add(_status);
        DockPanel.SetDock(footer, Dock.Bottom); _root.Children.Add(footer);
        StackPanel pages = Row(); pages.HorizontalAlignment = System.Windows.HorizontalAlignment.Center; pages.Margin = new Thickness(0, 8, 0, 0);
        pages.Children.Add(Action("▦ 日历", () => { _manage = false; Navigate(false); }));
        pages.Children.Add(Action("◷ 闹钟", () => { _manage = false; Navigate(true); }));
        pages.Children.Add(Action("☷ 行程管理", () => { Navigate(false); _manage = true; _editing = false; Render(); }));
        DockPanel.SetDock(pages, Dock.Bottom); _root.Children.Add(pages);
        _root.Children.Add(_body); _shell.Children.Add(_root); Content = _shell;
        _service.Changed += OnChanged;
        _clock.Tick += (_, _) => UpdateRemaining();
        IsVisibleChanged += (_, _) => { if (IsVisible) _clock.Start(); else _clock.Stop(); };
        Closed += (_, _) => { _clock.Stop(); _service.Changed -= OnChanged; };
        Render();
        UpdateDateStatus(_date);
    }
    public void Navigate(bool alarm) { _manage = false; if (_alarm != alarm) { Width = Math.Min(alarm ? 680 : 960, SystemParameters.WorkArea.Width - 24); Height = Math.Min(alarm ? 760 : 800, SystemParameters.WorkArea.Height - 24); } _alarm = alarm; _editing = false; Render(); Show(); Activate(); }
    private void OnChanged() { if (!_editing) Render(); }
    private async Task Execute(Action<ReminderBook> change)
    {
        try { await _service.ChangeAsync(change); _status.Text = "已保存到本机"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { _status.Text = ex is ArgumentException ? ex.Message : "保存失败，原数据未更改，请检查数据目录。"; throw; }
    }
    private static TextBlock Text(string value, double size = 15) => new()
    { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3), VerticalAlignment = VerticalAlignment.Center };
    private Button Action(string label, Action action)
    {
        Button button = new() { Content = label, Margin = new Thickness(3), Padding = new Thickness(15, 9, 15, 9), MinHeight = 38 };
        button.Click += (_, _) => action(); return button;
    }
    private Button AsyncAction(string label, Func<Task> action) => Action(label, async () =>
    { try { await action(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException) { _status.Text = ex is ArgumentException ? ex.Message : "操作失败，原数据已保留。"; } });
    private void UpdateDateStatus(DateTime day) => _status.Text = $"{day:yyyy年M月d日 dddd}  {CalendarLabels.FullLunar(day)}  {CalendarLabels.Get(day)}";
    private static Button Primary(Button b) { b.Background = PlannerTheme.Accent; b.Foreground = Brushes.White; return b; }
    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal };
    private void Render()
    {
        while (_shell.Children.Count > 1) _shell.Children.RemoveAt(_shell.Children.Count - 1);
        _root.IsEnabled = true;
        _remaining.Clear(); _header.Children.Clear();
        DockPanel tabs = new() { Margin = new Thickness(0, 8, 0, 20) };
        var add = Primary(Action("＋ 新增" + (_alarm ? "闹钟" : "行程"), () => { _occurrenceDate = null; Edit(null, !_alarm); }));
        DockPanel.SetDock(add, Dock.Right); tabs.Children.Add(add);
        tabs.Children.Add(Text(_manage ? "行程管理" : _alarm ? "闹钟" : "日历", 28));
        _body.VerticalScrollBarVisibility = _alarm ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        _header.Children.Add(tabs);
        if (_manage) RenderGroups(); else if (_alarm) RenderAlarms(); else RenderCalendar();
        UpdateDateStatus(_date); UpdateRemaining();
    }
    private void RenderCalendar()
    {
        bool week = _service.Book.WeekView;
        StackPanel nav = Row();
        nav.Children.Add(Action("‹", () => { MoveDate(week ? -7 : -1, week); }));
        nav.Children.Add(Text(week ? $"{WeekStart(_date):M月d日} — {WeekStart(_date).AddDays(6):M月d日}" : _date.ToString("yyyy 年 M 月"), 18));
        nav.Children.Add(Action("›", () => MoveDate(week ? 7 : 1, week)));
        nav.Children.Add(Action("今天", () => { _date = DateTime.Today; Render(); }));
        DockPanel navigation = new() { Margin = new Thickness(0, 0, 0, 10) }; StackPanel view = Row();
        var weekButton = AsyncAction("周", () => Execute(b => b.WeekView = true)); weekButton.Width = 58; view.Children.Add(week ? Primary(weekButton) : weekButton);
        var monthButton = AsyncAction("月", () => Execute(b => b.WeekView = false)); monthButton.Width = 58; view.Children.Add(week ? monthButton : Primary(monthButton));
        view.Children.Add(Action(_batch ? "完成调整" : "调整休息日", () => { _batch = !_batch; _selected.Clear(); Render(); }));
        DockPanel.SetDock(view, Dock.Right); navigation.Children.Add(view); navigation.Children.Add(nav); _header.Children.Add(navigation);
        if (_batch)
        {
            StackPanel restDays = Row(); restDays.Children.Add(Text("每周休息日"));
            foreach (DayOfWeek d in Days)
            {
                var day = d; var btn = AsyncAction("周" + "日一二三四五六"[(int)d], () => Execute(b => { var days = b.RestWeekdays.ToList(); if (!days.Remove(day)) days.Add(day); ReminderSchedule.SetRestWeekdays(b, days, DateTime.Now); }));
                if (_service.Book.RestWeekdays.Contains(d)) Primary(btn); restDays.Children.Add(btn);
            }
            _header.Children.Add(restDays);
            StackPanel presets = Row(); presets.Children.Add(Text("快捷选择"));
            presets.Children.Add(AsyncAction("周六日休息", () => Execute(b => ReminderSchedule.SetRestWeekdays(b, [DayOfWeek.Saturday, DayOfWeek.Sunday], DateTime.Now))));
            presets.Children.Add(AsyncAction("仅周日休息", () => Execute(b => ReminderSchedule.SetRestWeekdays(b, [DayOfWeek.Sunday], DateTime.Now)))); _header.Children.Add(presets);
            StackPanel batch = Row(); batch.Children.Add(Text($"已选 {_selected.Count} 天"));
            batch.Children.Add(AsyncAction("设为工作日", () => SetRest(false))); batch.Children.Add(AsyncAction("设为休息日", () => SetRest(true))); batch.Children.Add(AsyncAction("恢复常规", () => SetRest(null))); _header.Children.Add(batch);
        }
        CheckBox upcoming = new() { Name = "CalendarUpcoming", Content = "提前30分钟倒计时", IsChecked = _service.Book.ShowUpcoming, Margin = new Thickness(5, 6, 5, 8) };
        upcoming.Click += async (_, _) => { try { await Execute(b => b.ShowUpcoming = upcoming.IsChecked == true); } catch { } }; _header.Children.Add(upcoming);
        Grid grid = new(); for (int i = 0; i < 7; i++) grid.ColumnDefinitions.Add(new());
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (int i = 0; i < 7; i++) { var text = Text("周" + "一二三四五六日"[i], 13); text.TextAlignment = TextAlignment.Center; text.Margin = new Thickness(0, 8, 0, 8); Grid.SetColumn(text, i); grid.Children.Add(text); }
        DateTime start = week ? WeekStart(_date) : WeekStart(new DateTime(_date.Year, _date.Month, 1));
        int count = week ? 7 : ((int)(new DateTime(_date.Year, _date.Month, DateTime.DaysInMonth(_date.Year, _date.Month)) - start).TotalDays / 7 + 1) * 7;
        for (int row = 0; row < count / 7; row++) grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < count; i++)
        {
            DateTime day = start.AddDays(i);
            DockPanel content = new();
            bool rest = _service.Book.IsRest(day);
            string holiday = CalendarLabels.Get(day);
            Button dateButton = Action("", () => { _date = day; if (_batch) { if (!_selected.Add(day)) _selected.Remove(day); Render(); } else Edit(null, true); });
            dateButton.Padding = new Thickness(1); dateButton.Margin = new Thickness(0, 2, 0, 5); dateButton.MinHeight = week ? 58 : 26;
            dateButton.Background = Brushes.Transparent; dateButton.BorderThickness = new Thickness(0); dateButton.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
            DockPanel heading = new();
            bool manual = _service.Book.RestOverrides.ContainsKey(day.ToString("yyyy-MM-dd"));
            if (rest || manual || _selected.Contains(day))
            {
                TextBlock badge = Text(_selected.Contains(day) ? "✓" : rest ? "休" : "班", 10); badge.Background = PlannerTheme.Soft; badge.Foreground = PlannerTheme.Accent;
                DockPanel.SetDock(badge, Dock.Right); heading.Children.Add(badge);
            }
            TextBlock number = Text(day.Day.ToString(), 16); number.FontWeight = FontWeights.SemiBold;
            if (day == DateTime.Today) { number.Background = PlannerTheme.Accent; number.Foreground = Brushes.White; }
            DockPanel.SetDock(number, Dock.Left); heading.Children.Add(number);
            TextBlock lunar = Text(CalendarLabels.LunarDay(day), 11); lunar.Foreground = Brushes.SlateGray; DockPanel.SetDock(lunar, Dock.Left); heading.Children.Add(lunar);
            TextBlock festival = Text(holiday, 11); festival.Foreground = PlannerTheme.Accent; festival.TextWrapping = TextWrapping.NoWrap; festival.TextTrimming = TextTrimming.CharacterEllipsis; heading.Children.Add(festival);
            if (week)
            {
                StackPanel weekHead = new(); weekHead.Children.Add(Text(day.ToString("M月d日") + " · " + CalendarLabels.LunarDay(day), 13)); weekHead.Children.Add(Text(holiday + (rest ? "  休" : manual ? "  班" : ""), 11)); dateButton.Content = weekHead;
            }
            else dateButton.Content = heading;
            dateButton.ToolTip = day.ToString("yyyy年M月d日") + " " + CalendarLabels.FullLunar(day) + "\n" + holiday;
            dateButton.MouseEnter += (_, _) => UpdateDateStatus(day);
            System.Windows.Controls.ContextMenu menu = new();
            foreach (var choice in new[] { ("设为工作日", (bool?)false), ("设为休息日", (bool?)true), ("恢复常规作息", (bool?)null) })
            {
                System.Windows.Controls.MenuItem entry = new() { Header = choice.Item1 }; entry.Click += async (_, _) => { try { await Execute(b => ApplyRest(b, day, choice.Item2)); } catch { } }; menu.Items.Add(entry);
            }
            dateButton.ContextMenu = menu; DockPanel.SetDock(dateButton, Dock.Top); content.Children.Add(dateButton);
            StackPanel entries = new();
            foreach (ReminderItem item in _service.Book.Items.Where(item => item.Calendar && ReminderSchedule.OccursOn(item, _service.Book, day)).OrderBy(item => item.Start.TimeOfDay))
            {
                Button button = Action("", () => { _occurrenceDate = day; Edit(item, true); });
                TextBlock eventText = Text($"{item.Start:HH:mm} {item.Title}", 13);
                if (item.Enabled) eventText.Inlines.Add(new System.Windows.Documents.Run("  \u23F0") { FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"), Foreground = Brushes.Teal });
                if (week) { eventText.Text = $"{item.Start:HH:mm}" + (item.Enabled ? "  ⏰" : "") + "\n" + ReminderSchedule.FullContent(item); eventText.LineHeight = 23; }
                button.MinHeight = week ? 70 : 26; button.Content = eventText;
                if (!week) {
                    button.Content = null;
                    eventText.Text = $"{item.Start:HH:mm} {item.Title}";
                    DockPanel eventRow = new();
                    if (item.Enabled) { TextBlock icon = Text("⏰", 11); icon.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"); icon.Foreground = PlannerTheme.Accent; icon.Margin = new Thickness(1); DockPanel.SetDock(icon,Dock.Right); eventRow.Children.Add(icon); }
                    eventRow.Children.Add(eventText); button.Content = eventRow;
                } button.Background = new SolidColorBrush(new[] { System.Windows.Media.Color.FromRgb(221,245,249), System.Windows.Media.Color.FromRgb(226,245,236), System.Windows.Media.Color.FromRgb(239,234,252) }[(int)((uint)item.Id.GetHashCode() % 3)]); button.BorderThickness = new Thickness(0);
                eventText.MaxHeight = week ? 96 : 22;
                if (!week) { eventText.TextWrapping = TextWrapping.NoWrap; eventText.TextTrimming = TextTrimming.CharacterEllipsis; eventText.Margin = new Thickness(1); }
                button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
                button.Padding = week ? new Thickness(7, 8, 7, 8) : new Thickness(3, 3, 3, 3); button.Margin = new Thickness(1, 2, 1, 2);
                button.ToolTip = (item.Enabled ? "已设置到点提醒\n" : "") + ReminderSchedule.FullContent(item); entries.Children.Add(button);
            }
            content.Children.Add(new ScrollViewer { Content = entries, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            Border cell = new() { Child = content, BorderBrush = PlannerTheme.Line, BorderThickness = new Thickness(0.5), Padding = new Thickness(6),
                Background = _selected.Contains(day) ? Brushes.LightCyan : rest ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(245,249,250)) : Brushes.White };
            if (day < ReminderSchedule.MinimumDate || day > ReminderSchedule.MaximumDate) cell.IsEnabled = false;
            Grid.SetColumn(cell, i % 7); Grid.SetRow(cell, i / 7 + 1); grid.Children.Add(cell);
        }
        _body.Content = grid;
    }
    private static DateTime WeekStart(DateTime date) => date.Date.AddDays(-((int)date.DayOfWeek + 6) % 7);
    private void MoveDate(int offset, bool week)
    {
        DateTime next = week ? _date.AddDays(offset) : _date.AddMonths(offset);
        if (next >= ReminderSchedule.MinimumDate && next <= ReminderSchedule.MaximumDate) _date = next;
        Render();
    }
    private Task SetRest(bool? rest) => Execute(b => { foreach (DateTime date in _selected) ApplyRest(b, date, rest); });
    private static void ApplyRest(ReminderBook book, DateTime day, bool? rest)
        => ReminderSchedule.SetRestOverride(book, day, rest, DateTime.Now);

    private void RenderAlarms()
    {
        StackPanel list = new();
        StackPanel prefs = Row();
        CheckBox remaining = new() { Content = "显示剩余时间", IsChecked = _service.Book.ShowRemaining, Margin = new Thickness(5) };
        remaining.Click += async (_, _) => { try { await Execute(b => b.ShowRemaining = remaining.IsChecked == true); } catch { } };
        prefs.Children.Add(remaining); _header.Children.Add(prefs);
        foreach (ReminderItem item in _service.Book.Items.OrderBy(i => ReminderSchedule.Next(i, _service.Book, DateTime.Now) ?? DateTime.MaxValue))
        {
            DateTime? at = ReminderSchedule.Next(item, _service.Book, DateTime.Now);
            if (item.Calendar && (!item.Enabled || at == null)) continue;
            Grid card = new() { Margin = new Thickness(4) };
            card.ColumnDefinitions.Add(new() { Width = new GridLength(190) }); card.ColumnDefinitions.Add(new()); card.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            Button timeButton = Action(item.Relative ? TimeSpan.FromSeconds(item.DurationSeconds).ToString(@"hh\:mm\:ss") : item.Start.ToString("HH:mm"), () => { _occurrenceDate = null; Edit(item, item.Calendar); });
            timeButton.FontSize = item.Relative ? 32 : 38; timeButton.FontWeight = FontWeights.SemiBold; timeButton.BorderThickness = new Thickness(0); card.Children.Add(timeButton);
            StackPanel detail = new() { Margin = new Thickness(8, 4, 8, 4) }; Grid.SetColumn(detail, 1); card.Children.Add(detail);
            Button contentButton = Action(item.Title, () => { _occurrenceDate = null; Edit(item, item.Calendar); }); contentButton.BorderThickness = new Thickness(0); contentButton.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left; contentButton.FontSize = 18; contentButton.FontWeight = FontWeights.SemiBold; contentButton.Padding = new Thickness(3); contentButton.MinHeight = 28; contentButton.ToolTip = ReminderSchedule.FullContent(item); detail.Children.Add(contentButton);
            TextBlock origin = Text(item.Calendar ? "来自日历" : item.Relative ? "倒计时" : Repeats[(int)item.Repeat], 11); origin.Foreground = PlannerTheme.Accent; detail.Children.Add(origin);
            detail.Children.Add(Text(item.Repeat == ReminderRepeat.Weekly ? string.Join("、", item.Weekdays.Select(d => "周" + "日一二三四五六"[(int)d])) : item.Start.ToString("M月d日 HH:mm"), 12));
            if (at is DateTime due) { TextBlock text = Text("", 11); detail.Children.Add(text); _remaining.Add((text, due)); }
            else detail.Children.Add(Text(item.Enabled ? "已到期" : "已关闭", 11));
            StackPanel controls = new() { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(controls, 2); card.Children.Add(controls);
            CheckBox enabled = new() { Content = "", IsChecked = item.Enabled, Margin = new Thickness(8) };
            enabled.Style = (Style)FindResource("PlannerSwitch");
            enabled.Click += async (_, _) => { try { await Execute(b => { var x = b.Items.Single(i => i.Id == item.Id); x.Enabled = enabled.IsChecked == true; x.PendingAt = null; x.SnoozeUntil = null; x.CheckedThrough = DateTime.Now; }); } catch { } }; controls.Children.Add(enabled);
            System.Windows.Controls.ContextMenu menu = new();
            foreach (var label in new[] { "编辑", "删除", "完成提醒", "再来一次" })
            {
                if (label == "完成提醒" && item.PendingAt == null || label == "再来一次" && !item.Relative) continue;
                System.Windows.Controls.MenuItem entry = new() { Header = label };
                entry.Click += async (_, _) => { try { if(label == "编辑") { _occurrenceDate=null; Edit(item,item.Calendar); } else if(label == "删除") ConfirmDelete([item.Id], null); else await Execute(b => { var x=b.Items.FirstOrDefault(i=>i.Id==item.Id); if(x==null)return; if(label=="删除")b.Items.Remove(x); else if(label=="完成提醒")ReminderSchedule.Dismiss(x,DateTime.Now); else {x.Start=DateTime.Now.AddSeconds(x.DurationSeconds);x.CheckedThrough=DateTime.Now;x.PendingAt=null;x.SnoozeUntil=null;x.Enabled=true;} }); } catch { } }; menu.Items.Add(entry);
            }
            Button more = Action("⋯", () => { menu.IsOpen = true; }); more.ContextMenu = menu; menu.PlacementTarget = more; controls.Children.Add(more);
            list.Children.Add(new Border { Child = card, Background = Brushes.White, BorderBrush = PlannerTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(8), Margin = new Thickness(3, 6, 3, 6) });
        }
        if (list.Children.Count == 0) list.Children.Add(Text("暂无闹钟。可以新增，或为日历行程开启提醒。"));
        _body.Content = list;
    }
    internal static string Remaining(DateTime at)
    {
        TimeSpan left = at - DateTime.Now;
        if (left <= TimeSpan.Zero) return "已到时间";
        return left.TotalDays >= 1 ? $"还有 {(int)left.TotalDays} 天 {left.Hours} 小时" : $"剩余 {(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
    }
    private void UpdateRemaining() { foreach (var pair in _remaining) pair.Text.Text = _service.Book.ShowRemaining ? Remaining(pair.At) : ""; }

    private void EditWorkdays()
    {
        _editing = false; _alarm = false; _batch = true; Render();
    }
    private void Edit(ReminderItem? original, bool calendar)
    {
        _editing = true; _remaining.Clear();
        StackPanel panel = new() { Width = 510, MaxWidth = 510, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        DockPanel editorHeading = new() { Margin = new Thickness(0, 0, 0, 18) };
        var closeEditor = Action("×", () => { _editing = false; Render(); }); DockPanel.SetDock(closeEditor, Dock.Right); editorHeading.Children.Add(closeEditor);
        editorHeading.Children.Add(Text((original == null ? "新增" : "编辑") + (calendar ? "行程" : "闹钟"), 24)); panel.Children.Add(editorHeading);
        if(original != null && original.Repeat != ReminderRepeat.Once) { TextBlock groupHint = Text(GroupLabel(original) + " · 修改将应用到整组", 12); groupHint.Foreground = PlannerTheme.Accent; panel.Children.Add(groupHint); }
        TextBox title = new() { Name = "ReminderTitle", Text = original == null ? "" : ReminderSchedule.FullContent(original), MaxLength = 10122, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(4, 6, 4, 14) };
        panel.Children.Add(Text("行程内容")); panel.Children.Add(title);
        ComboBox mode = new() { Name = "ReminderMode", ItemsSource = new[] { "指定时间", "倒计时（最多 24 小时）" }, SelectedIndex = original?.Relative == true ? 1 : 0, Margin = new Thickness(4), IsEnabled = !calendar };
        if (!calendar) panel.Children.Add(mode);
        DateTime start = original?.Start ?? (calendar ? _date.Date + DateTime.Now.AddHours(1).TimeOfDay : DateTime.Now.AddHours(1));
        DatePicker date = new() { SelectedDate = start.Date, DisplayDateStart = ReminderSchedule.MinimumDate, DisplayDateEnd = ReminderSchedule.MaximumDate, Margin = new Thickness(4) };
        TextBox time = new() { Text = start.ToString("HH:mm"), Margin = new Thickness(4), Width = 120 };
        int seconds = original?.DurationSeconds ?? _service.Book.LastDurationSeconds;
        TextBox duration = new() { Name = "ReminderDuration", Text = $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}", Margin = new Thickness(4), Width = 120 };
        StackPanel when = Row(); when.Children.Add(date); when.Children.Add(Text("时间")); when.Children.Add(time);
        StackPanel after = Row(); after.Children.Add(Text("时:分:秒")); after.Children.Add(duration);
        panel.Children.Add(when); panel.Children.Add(after);
        ComboBox repeat = new() { Name = "ReminderRepeat", ItemsSource = Repeats, SelectedIndex = (int)(original?.Repeat ?? ReminderRepeat.Once), Margin = new Thickness(4) };
        StackPanel repeatRow = Row(); repeatRow.Margin = new Thickness(0, 8, 0, 8); repeatRow.Children.Add(Text("重复")); repeat.Width = 390; repeatRow.Children.Add(repeat); panel.Children.Add(repeatRow);
        var checks = Days.Select(d => new CheckBox { Content = "周" + "日一二三四五六"[(int)d], IsChecked = original?.Weekdays.Contains(d) == true, Margin = new Thickness(5) }).ToArray();
        StackPanel weekdays = Row(); foreach (var check in checks) weekdays.Children.Add(check); panel.Children.Add(weekdays);
        List<DateTime> chosenDates = original?.Dates.Count > 0 ? original.Dates.ToList() : [start.Date];
        StackPanel dateRow = Row(); TextBlock dateSummary = Text("");
        void UpdateDates() => dateSummary.Text = chosenDates.Count <= 3 ? string.Join("、", chosenDates.OrderBy(d => d).Select(d => d.ToString("M月d日"))) : $"已选 {chosenDates.Count} 天";
        UpdateDates(); dateRow.Children.Add(Text("日期")); dateRow.Children.Add(dateSummary);
        var modifyDates = Action("修改", () => { DateSelectionWindow picker = new(chosenDates, date.SelectedDate ?? start) { Owner = this }; if (picker.ShowDialog() == true) { chosenDates = picker.Selection.OrderBy(d => d).ToList(); UpdateDates(); } }); modifyDates.Name = "ModifyDates"; dateRow.Children.Add(modifyDates); panel.Children.Add(dateRow);
        CheckBox enabled = new() { Content = calendar ? "开启提醒（关联到闹钟）" : "开启闹钟", IsChecked = original?.Enabled ?? !calendar, Margin = new Thickness(6) };
        CheckBox sound = new() { Content = "到点播放短提示音", IsChecked = original?.Sound ?? true, Margin = new Thickness(6) };
        CheckBox countdown = new() { Name = "CalendarCountdown", Content = "提前30分钟倒计时", IsChecked = original?.ShowCountdown ?? true, Margin = new Thickness(6), Visibility = calendar ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed };
        enabled.Style = (Style)FindResource("PlannerSwitch");
        panel.Children.Add(new Border { Height = 1, Background = PlannerTheme.Line, Margin = new Thickness(0, 16, 0, 16) });
        panel.Children.Add(enabled); panel.Children.Add(countdown); panel.Children.Add(sound);
        panel.Children.Add(Text("关闭倒计时仍会到点提醒。", 12));
        void Visibility()
        {
            bool relative = mode.SelectedIndex == 1;
            when.Visibility = relative ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            after.Visibility = relative ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            repeat.IsEnabled = !relative;
            weekdays.Visibility = !relative && repeat.SelectedIndex == 2 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            dateRow.Visibility = !relative && repeat.SelectedIndex == 3 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            date.Visibility = repeat.SelectedIndex == 3 ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }
        mode.SelectionChanged += (_, _) => Visibility(); repeat.SelectionChanged += (_, _) => Visibility(); Visibility();
        Button save = AsyncAction("保存", async () =>
        {
            bool relative = mode.SelectedIndex == 1;
            if (!TimeSpan.TryParseExact(time.Text.Trim(), new[] { @"h\:mm", @"hh\:mm", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out TimeSpan clockTime) || clockTime.TotalDays >= 1)
                throw new ArgumentException("时间请输入 00:00～23:59。");
            int durationSeconds = _service.Book.LastDurationSeconds;
            if (relative)
            {
                string[] parts = duration.Text.Split(':');
                if (parts.Length != 3 || !int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m) || !int.TryParse(parts[2], out int s) || h < 0 || h > 24 || m < 0 || m > 59 || s < 0 || s > 59)
                    throw new ArgumentException("时长格式为 时:分:秒，最多 24:00:00。");
                durationSeconds = h * 3600 + m * 60 + s;
                if (durationSeconds < 1 || durationSeconds > 86400) throw new ArgumentException("时长为 1 秒到 24 小时。");
            }
            DateTime now = DateTime.Now;
            DateTime at = relative ? now.AddSeconds(durationSeconds) : (date.SelectedDate ?? throw new ArgumentException("请选择日期。")) + clockTime;
            ReminderRepeat rule = relative ? ReminderRepeat.Once : (ReminderRepeat)repeat.SelectedIndex;
            List<DateTime> selectedDates = chosenDates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
            if (rule == ReminderRepeat.Dates && selectedDates.Count > 0) at = selectedDates[0] + clockTime;
            if (enabled.IsChecked == true && rule == ReminderRepeat.Once && at <= now) throw new ArgumentException("开启的单次提醒需要选择未来时间；过去的行程可以关闭提醒后保存。");
            ReminderItem item = new() { Id = original?.Id ?? Guid.NewGuid(), Calendar = calendar, Title = title.Text.Trim().Split('\n')[0].Trim().Substring(0, Math.Min(120, title.Text.Trim().Split('\n')[0].Trim().Length)), Content = title.Text.Trim(), Notes = "",
                Start = at, Relative = relative, DurationSeconds = durationSeconds, Repeat = rule, Dates = selectedDates,
                Weekdays = Days.Where((_, i) => checks[i].IsChecked == true).ToList(), Enabled = enabled.IsChecked == true, Sound = sound.IsChecked == true, ShowCountdown = countdown.IsChecked == true, ExcludedDates = original?.ExcludedDates.ToList() ?? [], CheckedThrough = now };
            await Execute(b => { b.Items.RemoveAll(i => i.Id == item.Id); b.Items.Add(item); if (relative && item.Enabled) b.LastDurationSeconds = durationSeconds; });
            _editing = false; Render();
        });
        DockPanel buttons = new() { Margin = new Thickness(0, 20, 0, 0) }; panel.Children.Add(buttons);
        save.Name = "SaveReminder"; DockPanel.SetDock(save, Dock.Right); buttons.Children.Add(Primary(save));
        var cancel = Action("取消", () => { _editing = false; Render(); }); DockPanel.SetDock(cancel,Dock.Right); buttons.Children.Add(cancel);
        if (original != null) { var delete = Action(original.Repeat == ReminderRepeat.Once ? "删除" : "删除整组…", () => ConfirmDelete([original.Id], null)); delete.Name = "DeleteGroup"; delete.Foreground = Brushes.IndianRed; DockPanel.SetDock(delete,Dock.Left); buttons.Children.Add(delete);
            if(_occurrenceDate is DateTime selected && original.Calendar && original.Repeat != ReminderRepeat.Once) { var one = Action("仅删除这一天", () => ConfirmDelete([original.Id], selected)); one.Name = "DeleteOccurrence"; buttons.Children.Add(one); }
        }
        ShowOverlay(panel); title.Focus();
    }
}
