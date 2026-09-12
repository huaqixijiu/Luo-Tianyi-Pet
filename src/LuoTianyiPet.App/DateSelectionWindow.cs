using System.Windows;
using FontFamily = System.Windows.Media.FontFamily;
using System.Windows.Controls;
using System.Windows.Media;
using LuoTianyiPet.Core;
using Button = System.Windows.Controls.Button;
namespace LuoTianyiPet.App;
internal sealed class DateSelectionWindow : Window
{
    public HashSet<DateTime> Selection { get; }
    private DateTime _month;
    private readonly StackPanel _panel = new() { Margin = new Thickness(16) };
    public DateSelectionWindow(IEnumerable<DateTime> dates, DateTime month)
    {
        Selection = new(dates.Select(d => d.Date)); _month = new(month.Year, month.Month, 1);
        Title = "选择日期"; Width = 390; FontSize = 14; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        FontFamily = new FontFamily("Microsoft YaHei UI"); Background = Brushes.White; Content = _panel;
        PlannerTheme.Apply(this); Render();
    }
    private Button Make(string text, Action click) { Button b = new() { Content = text, Margin = new Thickness(2), Padding = new Thickness(7) }; b.Click += (_, _) => click(); return b; }
    private void Render()
    {
        _panel.Children.Clear();
        DockPanel nav = new(); var prev = Make("‹", () => Move(-1)); var next = Make("›", () => Move(1));
        DockPanel.SetDock(prev, Dock.Left); DockPanel.SetDock(next, Dock.Right); nav.Children.Add(prev); nav.Children.Add(next);
        nav.Children.Add(new TextBlock { Text = _month.ToString("yyyy年M月"), TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 17 }); _panel.Children.Add(nav);
        Grid grid = new(); for (int c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new()); for (int r = 0; r < 7; r++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (int c = 0; c < 7; c++) { TextBlock t = new() { Text = "一二三四五六日"[c].ToString(), TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 10, 4, 8) }; Grid.SetColumn(t,c); grid.Children.Add(t); }
        DateTime start = _month.AddDays(-((int)_month.DayOfWeek + 6) % 7);
        for (int i = 0; i < 42; i++)
        {
            DateTime day = start.AddDays(i); Button b = Make(day.Day.ToString(), () => { if (!Selection.Add(day)) Selection.Remove(day); Render(); });
            b.Name = "Date" + day.ToString("yyyyMMdd"); b.ToolTip = day.ToString("yyyy年M月d日") + " " + CalendarLabels.FullLunar(day);
            b.IsEnabled = day >= ReminderSchedule.MinimumDate && day <= ReminderSchedule.MaximumDate;
            b.Background = Selection.Contains(day) ? PlannerTheme.Accent : Brushes.Transparent; b.Foreground = Selection.Contains(day) ? Brushes.White : day.Month == _month.Month ? Brushes.DarkSlateGray : Brushes.Gray;
            Grid.SetColumn(b,i%7); Grid.SetRow(b,i/7+1); grid.Children.Add(b);
        }
        _panel.Children.Add(grid); StackPanel footer = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0,12,0,0) };
        footer.Children.Add(new TextBlock { Text = $"已选 {Selection.Count} 天", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) });
        footer.Children.Add(Make("清空", () => { Selection.Clear(); Render(); })); footer.Children.Add(Make("取消", () => DialogResult = false)); var ok = Make("确定", () => DialogResult = true); ok.Name = "ConfirmDates"; ok.Background = PlannerTheme.Accent; ok.Foreground = Brushes.White; footer.Children.Add(ok); _panel.Children.Add(footer);
    }
    private void Move(int delta) { DateTime next = _month.AddMonths(delta); if(next >= ReminderSchedule.MinimumDate && next <= ReminderSchedule.MaximumDate) { _month=next; Render(); } }
}
