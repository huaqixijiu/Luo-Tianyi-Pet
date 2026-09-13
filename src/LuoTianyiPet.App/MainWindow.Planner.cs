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
    private PetReminderCard? _reminderCard;
    private readonly List<(TextBlock Text,DateTime At)> _capsuleRemaining=[];
    private readonly DispatcherTimer _reminderDisplayTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly HashSet<string> _shownReminders = [];
    private bool _quickReminderExpanded;
    private ReminderBook? _presentedReminderBook;
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
            LocationChanged += (_,_) => QueuePlannerPosition();
            SizeChanged += (_,_) => QueuePlannerPosition();
            DpiChanged += (_,_) => QueuePlannerPosition();
            PreviewMouseUp += (_,_) => QueuePlannerPosition();
        }
        catch
        {
            _reminders.Dispose(); _reminders = null;
            _logger.Info("reminder.load_failed", "Reminder data retained; planner unavailable until recovery.");
        }
    }
    private bool _plannerPositionQueued;
    private void QueuePlannerPosition()
    {
        if(_plannerPositionQueued || _reminderCard==null || _isClosing)return;
        _plannerPositionQueued=true;
        Dispatcher.BeginInvoke(new Action(()=>{_plannerPositionQueued=false;RefreshReminderCard();}),DispatcherPriority.Background);
    }
    private void OpenPlanner(bool alarm)
    {
        _petQuickPanel?.Hide();
        if (_reminders == null || !_plannerReady)
        { System.Windows.MessageBox.Show("日历数据尚未就绪或无法读取，原文件已保留。请检查本地 reminders.json 与备份。", "天依日历"); return; }
        if (_plannerWindow == null)
        {
            _plannerWindow = new PlannerWindow(_reminders, alarm);
            _plannerWindow.ReminderSettingsRequested += OpenReminderSettings;
            _plannerWindow.Closed += (_, _) => _plannerWindow = null;
        }
        _plannerWindow.Navigate(alarm);
        if (_reminderStorageNotice != null)
        { System.Windows.MessageBox.Show(_plannerWindow, _reminderStorageNotice, "提醒数据"); _reminderStorageNotice = null; }
    }
    private void OnReminderDisplayTick(object? sender, EventArgs e) => RefreshReminderCard();
    private bool PlannerPresentationSafe(ForegroundApplicationSnapshot foreground)
    {
        return !_isClosing && !_systemSessionUnavailable && !_hiddenByUser &&
            _edgeDockSide == EdgeDockSide.None && foreground is { Succeeded: true, IsFullscreen: false } &&
            _stateMachine.VisualState.ContinuousState != PetContinuousState.HiddenForSafety &&
            foreground.ProcessName is not ("YuanShen" or "GenshinImpact" or "YuanShen.exe" or "GenshinImpact.exe");
    }
    private void RefreshReminderCard()
        => RefreshReminderCardCore(PlannerPresentationSafe(_foregroundApplicationProbe?.Query() ?? new(false, null, false)));
    private void RefreshReminderCardCore(bool safe)
    {
        if(_reminders==null||_isClosing)return;
        var book=_reminders.Book;DateTime now=DateTime.Now;
        var pending=book.Occurrences.Where(o=>o.Phase is ReminderPhase.Early or ReminderPhase.Due).OrderBy(o=>o.At).ToList();
        var capsules=book.Occurrences.Where(o=>o.Phase==ReminderPhase.AcknowledgedEarly&&o.At>now).OrderBy(o=>o.At).ToList();
        if(!safe||pending.Count+capsules.Count==0||_isWindowDragging&&pending.Count>0)
        { _reminderCard?.Hide();StopPlannerPresentation();if(pending.Count+capsules.Count==0)_quickReminderExpanded=false;return; }
        bool quick=pending.Count==0;
        if(quick)StopPlannerPresentation();else _quickReminderExpanded=false;
        _reminderDisplayTimer.Interval=TimeSpan.FromSeconds(quick&&!_quickReminderExpanded?10:1);
        string key=string.Join("|",pending.Concat(capsules).Select(o=>$"{o.RuleId}:{o.At.Ticks}:{o.Phase}:{o.Revision}"));
        if(!ReferenceEquals(book,_presentedReminderBook)){_reminderCardKey="";_presentedReminderBook=book;}
        if(_reminderCard==null)
        {
            _reminderCard=new PetReminderCard(this);
            _reminderCard.ToggleRequested+=()=>
            {
                _quickReminderExpanded=!_quickReminderExpanded;
                _reminderCard.SetExpanded(_quickReminderExpanded);
                _reminderDisplayTimer.Interval=TimeSpan.FromSeconds(_quickReminderExpanded?1:10);
                UpdateQuickReminderTimes();PositionReminderCard();
            };
            _reminderCard.GeometryChanged+=PositionReminderCard;
        }
        var work=GetQuickActionsWorkArea();var alpha=GetPetImageAlphaBoundsInWindow();
        double width=Math.Min(work.Width,quick?Numeric.Clamp(alpha.Width*1.35+16,316,376):440);
        if(Math.Abs(_reminderCard.Width-width)>1){_reminderCard.Width=width;_reminderCardKey="";}
        bool fresh=false;
        var validKeys=new HashSet<string>(pending.Select(o=>$"{o.RuleId}:{o.At.Ticks}:{o.Phase}:{o.Revision}"));_shownReminders.IntersectWith(validKeys);
        foreach(var k in validKeys)fresh|=_shownReminders.Add(k);
        if(key!=_reminderCardKey)
        {
            _reminderCardKey=key;_capsuleRemaining.Clear();StackPanel list=new(){Margin=new Thickness(18,quick?12:20,18,16)};
            foreach(var o in quick?capsules:pending)
            {
                var item=book.Items.FirstOrDefault(i=>i.Id==o.RuleId);if(item==null)continue;
                bool early=o.Phase==ReminderPhase.Early;
                list.Children.Add(new TextBlock{Text=ReminderEngine.Label(item),FontSize=quick?17:24,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,6)});
                if(!quick)list.Children.Add(new TextBlock{Text=o.At.ToString("HH:mm"),FontSize=15,Foreground=PlannerTheme.Muted,Margin=new Thickness(0,0,0,12)});
                TextBlock time=new(){Text=early||quick?"":"时间到了",Tag=quick,FontSize=quick?14:30,FontWeight=quick?FontWeights.Normal:FontWeights.SemiBold,Foreground=quick?PlannerTheme.Muted:new SolidColorBrush(Color.FromRgb(62,137,231)),Margin=new Thickness(0,0,0,14)};
                list.Children.Add(time);if(early||quick)_capsuleRemaining.Add((time,o.At));
                if(!quick&&!string.IsNullOrWhiteSpace(item.Notes))list.Children.Add(new TextBlock{Text=item.Notes,TextWrapping=TextWrapping.Wrap,MaxHeight=64,Foreground=PlannerTheme.Muted,Margin=new Thickness(0,0,0,16)});
                Grid actions=new();var labels=quick?new[]{"关闭本次提醒","查看详情"}:early?new[]{"知道了","稍后10分钟","本次不再提醒"}:new[]{"知道了","稍后10分钟"};
                for(int n=0;n<labels.Length;n++)
                {
                    string label=labels[n];actions.ColumnDefinitions.Add(new());
                    Button button=new(){Content=label,MinHeight=42,FontSize=13,Margin=new Thickness(n==0?0:6,0,0,0),Padding=new Thickness(5),Name=label=="知道了"?"AcknowledgeReminder":label=="稍后10分钟"?"SnoozeReminder":label=="查看详情"?"ViewReminder":"CancelOccurrence"};
                    bool primary=quick?label=="查看详情":label=="知道了";
                    if(primary){button.Background=new SolidColorBrush(Color.FromRgb(64,150,250));button.Foreground=Brushes.White;button.BorderThickness=new Thickness(0);}
                    button.Click+=async(_,_)=>
                    {
                        try
                        {
                            if(label=="查看详情") { OpenPlanner(!item.Calendar);_plannerWindow?.OpenItem(item.Id,o.At);return; }
                            button.IsEnabled=false;
                            await _reminders.ChangeAsync(b=>{if(label=="知道了")ReminderEngine.Acknowledge(b,o.RuleId,o.At,o.Phase);else if(label=="稍后10分钟")ReminderEngine.Snooze(b,o.RuleId,o.At,o.Phase,DateTime.Now);else ReminderEngine.Cancel(b,o.RuleId,o.At);});
                            _reminderCardKey="";RefreshReminderCard();
                        }
                        catch { button.Content="保存失败，重试";button.IsEnabled=true; }
                    };
                    Grid.SetColumn(button,n);actions.Children.Add(button);
                }
                list.Children.Add(actions);
                if((quick?capsules.Count:pending.Count)>1)list.Children.Add(new Border{Height=1,Background=PlannerTheme.Line,Margin=new Thickness(0,14,0,14)});
            }
            var first=capsules.FirstOrDefault();var firstItem=book.Items.FirstOrDefault(i=>i.Id==first?.RuleId);
            string summary=firstItem!=null?$"{ReminderEngine.Label(firstItem)} · {first!.At:HH:mm}"+(capsules.Count>1?$"  〔{capsules.Count}〕":""):"提醒";
            _reminderCard.Present(summary,list,quick,!quick||_quickReminderExpanded);
        }
        UpdateQuickReminderTimes();
        PositionReminderCard();_reminderCard.Show();_reminderCard.UpdateLayout();PositionReminderCard();
        if(!quick)
        {
            _plannerAlarmTopmost ??= AcquireTransientTopmost();
            if(fresh)ReminderAudio.Play(book.Preferences);
            if(!book.Preferences.Sound)ReminderAudio.Stop();
            if(book.Preferences.Animation)PlayPlannerAnimation();else StopPlannerAnimation();
        }
    }
    private void UpdateQuickReminderTimes()
    {
        foreach(var entry in _capsuleRemaining)
        {
            double minutes=Math.Max(0,Math.Ceiling((entry.At-DateTime.Now).TotalMinutes));
            string remaining=minutes<=0?"时间到了":$"还有{minutes:0}分钟";
            entry.Text.Text=entry.Text.Tag is true?$"{entry.At:HH:mm} · {remaining}":remaining;
        }
    }
    private bool _positioningReminder;
    private void PositionReminderCard()
    {
        if(_reminderCard==null||_isClosing||_positioningReminder)return;
        _positioningReminder=true;
        try
        {
            var work=GetQuickActionsWorkArea();var alpha=GetPetImageAlphaBoundsInWindow();
            var pet=new DesktopRectangle(Left+alpha.Left,Top+alpha.Top,alpha.Width,alpha.Height);
            var target=ReminderPlacement.Resolve(pet,work,_reminderCard.Width,Math.Max(18,_reminderCard.TargetHeight),2);
            // Eight DIPs of shadow inset + two DIPs outside = ten visible DIPs.
            bool above=target.Bottom<=pet.Top;
            double available=above?pet.Top-work.Top-2:work.Bottom-pet.Bottom-2;
            _reminderCard.LimitHeight(available);
            double actual=Math.Min(_reminderCard.MaxHeight,Math.Max(18,_reminderCard.ActualHeight));
            _reminderCard.Left=target.Left;
            _reminderCard.Top=above?target.Bottom-actual:target.Top;
        }
        finally { _positioningReminder=false; }
    }
    private void PlayPlannerAnimation()
    {
        // Called only from the already safety-checked presentation branch.
        if(_plannerAlarmReaction is Guid token && _stateMachine.ActiveReactionToken==token)return;
        CancelBunChase(restorePosition:true,restoreContinuousAnimation:false);
        CancelTimeGreetingPresentation(false,"Alarm is ringing.");
        StopClassicSpinDance(false,"planner.alarm");
        CancelCrystalLongIdle();_bodyReactionMotion.Cancel();ResetBodyReactionMirror();
        DateTimeOffset now=DateTimeOffset.Now;
        var outcome=_stateMachine.TryStartReaction(new ReactionRequest("alarm-tenth-birthday-coming",ReactionPriority.Alarm,now.AddSeconds(ReminderEngine.MaximumRoundSeconds),"planner:reminder",BlocksDisplayModeToggle:true),now);
        if(outcome.Result is ReactionStartResult.Started or ReactionStartResult.Replaced){_plannerAlarmReaction=outcome.Token;PlayAnimation("alarm-tenth-birthday-coming");}
    }
    private Guid? _plannerAlarmReaction,_plannerAlarmTopmost;
    private void StopPlannerAnimation()
    {
        if(_plannerAlarmReaction is Guid token){_plannerAlarmReaction=null;if(_stateMachine.CompleteReaction(token,DateTimeOffset.Now)&&!_isClosing)PlayResolvedContinuousAnimation();}
    }
    private void StopPlannerPresentation(){ReminderAudio.Stop();StopPlannerAnimation();ReleaseTransientTopmost(_plannerAlarmTopmost);_plannerAlarmTopmost=null;_shownReminders.Clear();}
    private bool _openPlannerNotificationSettings;
    private void OpenReminderSettings()
    {
        if(_settingsWindow!=null){_settingsWindow.NavigateNotifications();_settingsWindow.Activate();return;}
        _openPlannerNotificationSettings=true;ShowSettingsDialog();
    }
    private void ClosePlanner()
    {
        _reminderDisplayTimer.Stop(); _reminderDisplayTimer.Tick -= OnReminderDisplayTick;
        StopPlannerPresentation(); _plannerWindow?.Close(); _reminderCard?.Close(); _reminders?.Dispose();
    }
}
