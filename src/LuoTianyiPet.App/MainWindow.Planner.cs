using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;
using Button = System.Windows.Controls.Button;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private ReminderService? _reminders;
    private PlannerWindow? _plannerWindow;
    private Window? _reminderCard;
    private TextBlock? _reminderSummary;
    private readonly DispatcherTimer _reminderDisplayTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly HashSet<string> _shownReminders = [];
    private DateTime _reminderExpandedUntil;
    private ReminderBook? _presentedReminderBook;
    private Guid? _reminderUndoId;
    private DateTime? _reminderUndoSkipped, _reminderUndoHidden;
    private DateTime _reminderUndoUntil;
    private string _reminderCardKey = "";
    private string? _reminderStorageNotice;
    private bool _plannerReady;
    private async void InitializePlanner()
    {
        if (!_persistSettings)
        {
            if (Environment.GetCommandLineArgs().Contains("--qa-planner")) await RunPlannerQaAsync();
            return;
        }
        _reminders = new ReminderService(((App)System.Windows.Application.Current).ReminderPaths);
        _reminders.Failed += message => { _reminderStorageNotice = message; _logger.Info("reminder.storage", "Reminder storage needs attention."); };
        try
        {
            await _reminders.LoadAsync();
            if (_isClosing) { _reminders.Dispose(); return; }
            _plannerReady = true;
            _reminders.Changed += RefreshReminderCard;
            _reminderDisplayTimer.Tick += OnReminderDisplayTick;
            _reminderDisplayTimer.Start();
        }
        catch
        {
            _reminders.Dispose(); _reminders = null;
            _logger.Info("reminder.load_failed", "Reminder data retained; planner unavailable until recovery.");
        }
    }
    private void OpenPlanner(bool alarm)
    {
        _petQuickPanel?.Hide();
        if (_reminders == null || !_plannerReady)
        { System.Windows.MessageBox.Show("日历数据尚未就绪或无法读取，原文件已保留。请检查本地 reminders.json 与备份。", "天依日历"); return; }
        if (_plannerWindow == null)
        {
            _plannerWindow = new PlannerWindow(_reminders, alarm);
            _plannerWindow.Closed += (_, _) => _plannerWindow = null;
        }
        _plannerWindow.Navigate(alarm);
        if (_reminderStorageNotice != null)
        { System.Windows.MessageBox.Show(_plannerWindow, _reminderStorageNotice, "提醒数据"); _reminderStorageNotice = null; }
    }
    private void OnReminderDisplayTick(object? sender, EventArgs e) => RefreshReminderCard();
    private bool PlannerPresentationSafe(ForegroundApplicationSnapshot foreground)
    {
        return !_isClosing && !_systemSessionUnavailable && !_hiddenByUser && !_isWindowDragging &&
            _edgeDockSide == EdgeDockSide.None && foreground is { Succeeded: true, IsFullscreen: false } &&
            _stateMachine.VisualState.ContinuousState != PetContinuousState.HiddenForSafety &&
            foreground.ProcessName is not ("YuanShen" or "GenshinImpact" or "YuanShen.exe" or "GenshinImpact.exe");
    }
    private void RefreshReminderCard()
        => RefreshReminderCardCore(PlannerPresentationSafe(_foregroundApplicationProbe?.Query() ?? new(false, null, false)));
    private void RefreshReminderCardCore(bool safe)
    {
        if (_reminders == null || _isClosing) return;
        var book = _reminders.Book;
        if (!ReferenceEquals(book, _presentedReminderBook)) { _reminderCardKey = ""; _presentedReminderBook = book; }
        DateTime now = DateTime.Now;
        var pending = book.Items.Where(i => i.Enabled && i.PendingAt != null && i.SnoozeUntil == null).ToList();
        var future = book.Items.Select(i => (Item: i, At: ReminderSchedule.Upcoming(i, book, now)))
            .Where(x => x.At != null).OrderBy(x => x.At).FirstOrDefault();
        bool undo = _reminderUndoId != null && now < _reminderUndoUntil;
        bool hasContent = pending.Count > 0 || future.At != null || undo;
        _reminderDisplayTimer.Interval = TimeSpan.FromSeconds(hasContent ? 1 : book.Items.Count == 0 ? 60 : 10);
        _shownReminders.IntersectWith(pending.Select(i => i.Id + ":" + i.PendingAt!.Value.Ticks));
        if (!hasContent || !safe) { _reminderCard?.Hide(); return; }
        bool fresh = false, sound = false;
        foreach (var item in pending)
            if (_shownReminders.Add(item.Id + ":" + item.PendingAt!.Value.Ticks)) { fresh = true; sound |= item.Sound; }
        if (fresh) _reminderExpandedUntil = now.AddSeconds(30);
        bool expanded = (pending.Count > 0 || future.At != null) && (now < _reminderExpandedUntil || _reminderCard?.IsMouseOver == true);
        string key = string.Join("|", pending.Select(i => i.Id + ":" + i.PendingAt + ":" + i.Title + ":" + i.Notes)) + expanded + ":" + future.Item?.Id + ":" + future.At + ":" + undo;
        if (_reminderCard == null)
        {
            _reminderCard = new Window { Title = "天依提醒", Width = 340, FontSize = 14, SizeToContent = SizeToContent.Height, MaxHeight = 410,
                WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false,
                Topmost = true, AllowsTransparency = true, Background = Brushes.Transparent, FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI") };
            PlannerTheme.Apply(_reminderCard);
        }
        if (key != _reminderCardKey || _reminderSummary == null)
        {
            _reminderCardKey = key;
            StackPanel panel = new() { Margin = new Thickness(16) };
            Button summary = new() { Padding = new Thickness(6), HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left };
            _reminderSummary = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 15, Foreground = PlannerTheme.Accent };
            summary.Content = _reminderSummary;
            summary.Click += (_, _) => { _reminderExpandedUntil = DateTime.Now.AddSeconds(30); _reminderCardKey = ""; RefreshReminderCard(); };
            panel.Children.Add(summary);
            if (undo)
            {
                Button undoButton = new() { Content = "已处理本次提醒 · 撤销", Margin = new Thickness(3), Name = "UndoEarlyReminder" };
                undoButton.Click += async (_, _) => { try { var id = _reminderUndoId; await _reminders.ChangeAsync(b => { var item = b.Items.FirstOrDefault(i => i.Id == id); if(item != null) { item.SkippedAt = _reminderUndoSkipped; item.HiddenCountdownAt = _reminderUndoHidden; } }); _reminderUndoId = null; _reminderCardKey = ""; RefreshReminderCard(); } catch { undoButton.Content = "保存失败，请重试"; } }; panel.Children.Add(undoButton);
            }
            if (expanded && pending.Count == 0 && future.Item != null && future.At is DateTime upcomingAt)
            {
                panel.Children.Add(new ScrollViewer { Content = new TextBlock { Text = ReminderSchedule.FullContent(future.Item), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) }, MaxHeight = 130, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
                StackPanel actions = new() { Orientation = Orientation.Horizontal };
                foreach (bool skip in new[] { true, false })
                {
                    Button button = new() { Name = skip ? "SkipThisReminder" : "HideThisCountdown", Content = skip ? "本次不再提醒" : "隐藏倒计时", Margin = new Thickness(3), Padding = new Thickness(10) };
                    if(skip) { button.Background = PlannerTheme.Accent; button.Foreground = Brushes.White; }
                    button.Click += async (_, _) => { try { await ActOnUpcoming(future.Item.Id, upcomingAt, skip); } catch { button.Content = "保存失败，请重试"; } }; actions.Children.Add(button);
                }
                panel.Children.Add(actions);
            }
            if (expanded && pending.Count > 0)
            {
                StackPanel items = new();
                foreach (var item in pending)
                {
                    items.Children.Add(new TextBlock { Text = item.Title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3, 8, 3, 3) });
                    items.Children.Add(new TextBlock { Text = $"{item.PendingAt:MM-dd HH:mm} · {(item.Calendar ? "来自日历" : "闹钟")}", Margin = new Thickness(3) });
                    items.Children.Add(new TextBlock { Text = ReminderSchedule.FullContent(item), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3) });
                    StackPanel actions = new() { Orientation = Orientation.Horizontal };
                    foreach (bool snooze in new[] { false, true })
                    {
                        Button button = new() { Content = snooze ? "5 分钟后提醒" : "完成", Padding = new Thickness(6), Margin = new Thickness(3) };
                        button.Click += async (_, _) =>
                        {
                            try
                            {
                                await _reminders.ChangeAsync(b =>
                                {
                                    var current = b.Items.FirstOrDefault(i => i.Id == item.Id); if (current == null) return;
                                    if (snooze) { current.SnoozeUntil = DateTime.Now.AddMinutes(5); current.PendingAt = null; }
                                    else ReminderSchedule.Dismiss(current, DateTime.Now);
                                });
                            }
                            catch { button.Content = "保存失败，请重试"; }
                        };
                        actions.Children.Add(button);
                    }
                    items.Children.Add(actions);
                    if (item.Relative)
                    {
                        Button again = new() { Content = "再来一次", Padding = new Thickness(6), Margin = new Thickness(3), HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
                        again.Click += async (_, _) =>
                        {
                            try { await _reminders.ChangeAsync(b => { var x = b.Items.FirstOrDefault(i => i.Id == item.Id); if (x == null) return; x.Start = DateTime.Now.AddSeconds(x.DurationSeconds); x.CheckedThrough = DateTime.Now; x.PendingAt = null; x.SnoozeUntil = null; x.Enabled = true; }); }
                            catch { again.Content = "保存失败，请重试"; }
                        };
                        items.Children.Add(again);
                    }
                }
                panel.Children.Add(new ScrollViewer { Content = items, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
                Button collapse = new() { Content = "收起", Margin = new Thickness(3), Padding = new Thickness(3) };
                collapse.Click += (_, _) => { _reminderExpandedUntil = DateTime.MinValue; _reminderCardKey = ""; _reminderCard.Hide(); };
                panel.Children.Add(collapse);
            }
            _reminderCard.Content = new Border { Child = panel, Background = Brushes.White, CornerRadius = new CornerRadius(10), BorderBrush = PlannerTheme.Line, BorderThickness = new Thickness(1) };
        }
        _reminderSummary!.Text = pending.Count > 0 ? $"🔔 {pending.Count} 项待处理提醒" : future.Item != null && future.At != null ? $"⏰ {future.Item.Title} · {PlannerWindow.Remaining(future.At.Value)}" : "本次提醒已处理";
        DesktopRectangle work = GetQuickActionsWorkArea();
        _reminderCard.Show(); _reminderCard.UpdateLayout();
        DesktopRectangle pet = GetPetImageAlphaBoundsInWindow();
        double center = Left + pet.Left + pet.Width / 2, bottom = Top + pet.Bottom;
        if (MediaControls.IsVisible && MediaControls.Opacity > 0.05)
        {
            Point island = MediaControls.TranslatePoint(new Point(MediaControls.ActualWidth / 2, MediaControls.ActualHeight), this);
            center = Left + island.X; bottom = Math.Max(bottom, Top + island.Y);
        }
        _reminderCard.Left = Numeric.Clamp(center - _reminderCard.ActualWidth / 2, work.Left, Math.Max(work.Left, work.Right - _reminderCard.ActualWidth));
        double y = bottom + 6;
        if (y + _reminderCard.ActualHeight > work.Bottom) y = Top + pet.Top - _reminderCard.ActualHeight - 6;
        _reminderCard.Top = Numeric.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - _reminderCard.ActualHeight));
        if (sound) { try { System.Media.SystemSounds.Asterisk.Play(); } catch { } }
    }
    private async Task ActOnUpcoming(Guid id, DateTime at, bool skip)
    {
        if (_reminders == null) return;
        DateTime? oldSkip = null, oldHidden = null;
        await _reminders.ChangeAsync(b => {
            var item = b.Items.FirstOrDefault(i => i.Id == id); if (item == null) return;
            oldSkip = item.SkippedAt; oldHidden = item.HiddenCountdownAt;
            if (skip) ReminderSchedule.SkipOccurrence(item, at); else item.HiddenCountdownAt = at;
        });
        _reminderUndoId = id; _reminderUndoSkipped = oldSkip; _reminderUndoHidden = oldHidden; _reminderUndoUntil = DateTime.Now.AddSeconds(8);
        _reminderExpandedUntil = DateTime.MinValue; _reminderCardKey = ""; RefreshReminderCard();
    }
    private void ClosePlanner()
    {
        _reminderDisplayTimer.Stop(); _reminderDisplayTimer.Tick -= OnReminderDisplayTick;
        _plannerWindow?.Close(); _reminderCard?.Close(); _reminders?.Dispose();
    }
}
