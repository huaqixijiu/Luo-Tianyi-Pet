using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LuoTianyiPet.Core;
using CheckBox = System.Windows.Controls.CheckBox;
namespace LuoTianyiPet.App;
internal sealed partial class PlannerWindow
{
    private static string GroupLabel(ReminderItem item) => item.Repeat == ReminderRepeat.Dates
        ? $"同一组行程 · {item.Dates.Distinct().Count()} 个日期"
        : item.Repeat == ReminderRepeat.Once ? "单次行程" : "重复行程 · " + Repeats[(int)item.Repeat];

    private void ShowOverlay(UIElement content)
    {
        Grid overlay = new() { Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(95, 47, 72, 88)) };
        Border card = new() { Background = Brushes.White, CornerRadius = new CornerRadius(14), Padding = new Thickness(24), BorderBrush = PlannerTheme.Line, BorderThickness = new Thickness(1), HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16) };
        ScrollViewer scroll = new() { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = Math.Max(250, ActualHeight - 130) }; card.Child = scroll; overlay.Children.Add(card);
        _root.IsEnabled = false;
        if (_shell.Children.Count > 1) _shell.Children[_shell.Children.Count - 1].IsEnabled = false;
        _shell.Children.Add(overlay);
    }
    private void CloseTopOverlay()
    {
        if (_shell.Children.Count > 1) _shell.Children.RemoveAt(_shell.Children.Count - 1);
        _shell.Children[_shell.Children.Count - 1].IsEnabled = true;
    }
    private void ConfirmDelete(IEnumerable<Guid> ids, DateTime? day)
    {
        Guid[] selected = ids.Distinct().ToArray();
        var items = _service.Book.Items.Where(i => selected.Contains(i.Id)).ToList();
        if (items.Count == 0) return;
        StackPanel panel = new() { Width = 440 };
        panel.Children.Add(Text(day == null ? "删除整组行程？" : "仅删除这一天？", 23));
        string impact = day is DateTime date ? $"仅移除 {date:M月d日} 的行程及提醒，其他日期保持不变。"
            : $"将删除 {items.Count} 组行程，以及它们关联的全部日期和提醒。";
        panel.Children.Add(Text(impact));
        if (items.Count == 1) panel.Children.Add(Text(GroupLabel(items[0]), 13));
        StackPanel actions = Row(); actions.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
        actions.Children.Add(Action("取消", CloseTopOverlay));
        var confirm = AsyncAction(day == null ? "确认删除整组" : "确认删除这一天", async () =>
        {
            await Execute(b => { if (day is DateTime d) ReminderSchedule.DeleteDate(b, selected[0], d); else ReminderSchedule.DeleteGroups(b, selected); });
            _selectedGroups.Clear(); _editing = false; Render();
        }); confirm.Name = "ConfirmGroupDelete"; confirm.Background = Brushes.IndianRed; confirm.Foreground = Brushes.White; actions.Children.Add(confirm); panel.Children.Add(actions); ShowOverlay(panel);
    }
    private void RenderGroups()
    {
        _body.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        var groups = _service.Book.Items.Where(i => i.Calendar).OrderBy(i => i.Start).ToList();
        _selectedGroups.IntersectWith(groups.Select(i => i.Id));
        StackPanel tools = Row();
        tools.Children.Add(Text($"共 {groups.Count} 组 · 已选 {_selectedGroups.Count} 组", 14));
        tools.Children.Add(Action("全选", () => { foreach(var item in groups) _selectedGroups.Add(item.Id); Render(); }));
        tools.Children.Add(Action("取消选择", () => { _selectedGroups.Clear(); Render(); }));
        var delete = Action("删除选中组…", () => ConfirmDelete(_selectedGroups.ToArray(), null)); delete.IsEnabled = _selectedGroups.Count > 0; delete.Name = "DeleteSelectedGroups"; tools.Children.Add(delete); _header.Children.Add(tools);
        _header.Children.Add(Text("多个日期只列为一组，可统一修改日期或整组删除。", 13));
        StackPanel list = new();
        foreach (ReminderItem item in groups)
        {
            DockPanel card = new() { Margin = new Thickness(16) };
            CheckBox selected = new() { IsChecked = _selectedGroups.Contains(item.Id), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "选择整组" };
            selected.Click += (_, _) => { if(selected.IsChecked == true) _selectedGroups.Add(item.Id); else _selectedGroups.Remove(item.Id); Render(); }; DockPanel.SetDock(selected,Dock.Left); card.Children.Add(selected);
            StackPanel actions = Row(); actions.Children.Add(Action("修改整组", () => { _occurrenceDate = null; Edit(item,true); })); actions.Children.Add(Action("删除整组…", () => ConfirmDelete([item.Id],null))); DockPanel.SetDock(actions,Dock.Right); card.Children.Add(actions);
            StackPanel info = new(); info.Children.Add(Text(item.Title,17)); TextBlock badge = Text(GroupLabel(item),12); badge.Foreground = PlannerTheme.Accent; info.Children.Add(badge);
            string dates = item.Repeat == ReminderRepeat.Dates ? string.Join("、",item.Dates.OrderBy(d=>d).Take(6).Select(d=>d.ToString("M/d"))) + (item.Dates.Count > 6 ? " …" : "") : item.Start.ToString("yyyy/M/d HH:mm"); info.Children.Add(Text(dates,13)); card.Children.Add(info);
            list.Children.Add(new Border { Child = card, Background = Brushes.White, BorderBrush = PlannerTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 6, 0, 6) });
        }
        if(groups.Count == 0) list.Children.Add(Text("暂无行程。新建后可在这里按组管理。",16));
        _body.Content = list;
    }
}
