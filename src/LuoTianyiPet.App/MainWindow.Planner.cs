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
    private readonly List<(TextBlock Text,DateTime At)> _capsuleRemaining=[];
    private readonly DispatcherTimer _reminderDisplayTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly HashSet<string> _shownReminders = [];
    private DateTime _reminderExpandedUntil;
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
        return !_isClosing && !_systemSessionUnavailable && !_hiddenByUser && !_isWindowDragging &&
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
        if(!safe||pending.Count+capsules.Count==0){_reminderCard?.Hide();StopPlannerPresentation();return;}
        if(pending.Count==0)StopPlannerPresentation();
        bool expanded=pending.Count>0||now<_reminderExpandedUntil;
        // Collapsed capsules contain static time and count, so no per-second text churn.
        _reminderDisplayTimer.Interval=TimeSpan.FromSeconds(expanded?1:10);
        string key=string.Join("|",pending.Concat(capsules).Select(o=>$"{o.RuleId}:{o.At.Ticks}:{o.Phase}:{o.Revision}"))+expanded;
        if(!ReferenceEquals(book,_presentedReminderBook)){_reminderCardKey="";_presentedReminderBook=book;}
        if(_reminderCard==null){_reminderCard=new Window{Title="天依提醒",Width=expanded?360:230,FontSize=14,SizeToContent=SizeToContent.Height,MaxHeight=440,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,AllowsTransparency=true,Background=Brushes.Transparent,FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI")};PlannerTheme.Apply(_reminderCard);}
        bool fresh=false;
        var validKeys=new HashSet<string>(pending.Select(o=>$"{o.RuleId}:{o.At.Ticks}:{o.Phase}:{o.Revision}"));_shownReminders.IntersectWith(validKeys);
        foreach(var k in validKeys)fresh|=_shownReminders.Add(k);
        if(key!=_reminderCardKey)
        {
            _reminderCardKey=key;_capsuleRemaining.Clear();_reminderCard.Width=expanded?360:230;StackPanel panel=new(){Margin=new Thickness(12)};
            _reminderSummary=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=PlannerTheme.Accent,FontSize=14};
            var first=capsules.FirstOrDefault();var firstItem=book.Items.FirstOrDefault(i=>i.Id==first?.RuleId);
            _reminderSummary.Text=pending.Count>0?$"{pending.Count} 项提醒":firstItem!=null?$"{ReminderEngine.Label(firstItem)} · {first!.At:HH:mm}"+(capsules.Count>1?$"  {capsules.Count}":""):"提醒";
            _reminderSummary.Inlines.InsertBefore(_reminderSummary.Inlines.FirstInline,new System.Windows.Documents.InlineUIContainer(PlannerTheme.Bell()));
            Button summary=new(){Content=_reminderSummary,BorderThickness=new Thickness(0),Padding=new Thickness(3)};summary.Click+=(_,_)=>{_reminderExpandedUntil=DateTime.Now.AddSeconds(30);_reminderCardKey="";RefreshReminderCard();};panel.Children.Add(summary);
            if(expanded)
            {
                StackPanel list=new();
                foreach(var o in pending.Concat(capsules))
                {
                    var item=book.Items.FirstOrDefault(i=>i.Id==o.RuleId);if(item==null)continue;
                    bool early=o.Phase==ReminderPhase.Early, capsule=o.Phase==ReminderPhase.AcknowledgedEarly;
                    list.Children.Add(new TextBlock{Text=ReminderEngine.Label(item),FontWeight=FontWeights.SemiBold,Margin=new Thickness(3,12,3,4),TextWrapping=TextWrapping.Wrap});
                    var timeText=new TextBlock{Text=capsule||early?$"{o.At:HH:mm} · {PlannerWindow.Remaining(o.At)}":"时间到了",Foreground=PlannerTheme.Accent,Margin=new Thickness(3)};list.Children.Add(timeText);if(capsule||early)_capsuleRemaining.Add((timeText,o.At));
                    if(!string.IsNullOrWhiteSpace(item.Notes))list.Children.Add(new TextBlock{Text=item.Notes,TextWrapping=TextWrapping.Wrap,MaxHeight=90,Margin=new Thickness(3)});
                    WrapPanel actions=new();
                    foreach(var label in capsule?new[]{"关闭本次提醒","查看详情"}:early?new[]{"知道了","稍后10分钟","本次不再提醒"}:new[]{"知道了","稍后10分钟"})
                    {
                        Button b=new(){Content=label,Margin=new Thickness(3),Padding=new Thickness(7),Name=label=="知道了"?"AcknowledgeReminder":label=="稍后10分钟"?"SnoozeReminder":label=="查看详情"?"ViewReminder":"CancelOccurrence"};
                        b.Click+=async(_,_)=>{try{if(label=="查看详情"){OpenPlanner(!item.Calendar);_plannerWindow?.OpenItem(item.Id);return;}await _reminders.ChangeAsync(book=>{if(label=="知道了")ReminderEngine.Acknowledge(book,o.RuleId,o.At,o.Phase);else if(label=="稍后10分钟")ReminderEngine.Snooze(book,o.RuleId,o.At,o.Phase,DateTime.Now);else ReminderEngine.Cancel(book,o.RuleId,o.At);});_reminderCardKey="";RefreshReminderCard();}catch{b.Content="保存失败，请重试";}};actions.Children.Add(b);
                    }
                    list.Children.Add(actions);
                }
                panel.Children.Add(new ScrollViewer{Content=list,MaxHeight=350,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
            }
            _reminderCard.Content=new Border{Child=panel,Background=Brushes.White,CornerRadius=new CornerRadius(12),BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1)};
        }
        foreach(var remaining in _capsuleRemaining)remaining.Text.Text=$"{remaining.At:HH:mm} · {PlannerWindow.Remaining(remaining.At)}";
        _reminderCard.Show();_reminderCard.UpdateLayout();
        var work=GetQuickActionsWorkArea();var alpha=GetPetImageAlphaBoundsInWindow();double w=_reminderCard.ActualWidth,h=_reminderCard.ActualHeight;
        var position=ReminderPlacement.Resolve(new DesktopRectangle(Left+alpha.Left,Top+alpha.Top,alpha.Width,alpha.Height),work,w,h);
        if(pending.Count>0)
        {
            double center=Left+alpha.Left+alpha.Width/2,bottom=Top+alpha.Bottom;
            if(MediaControls.IsVisible&&MediaControls.Opacity>0.05){Point island=MediaControls.TranslatePoint(new Point(MediaControls.ActualWidth/2,MediaControls.ActualHeight),this);center=Left+island.X;bottom=Math.Max(bottom,Top+island.Y);}
            double y=bottom+6;if(y+h>work.Bottom)y=Top+alpha.Top-h-6;
            position=new DesktopRectangle(Numeric.Clamp(center-w/2,work.Left,Math.Max(work.Left,work.Right-w)),Numeric.Clamp(y,work.Top,Math.Max(work.Top,work.Bottom-h)),w,h);
        }
        _reminderCard.Left=position.Left;_reminderCard.Top=position.Top;
        if(pending.Count>0)
        {
            _plannerAlarmTopmost ??= AcquireTransientTopmost();
            if(fresh)ReminderAudio.Play(book.Preferences);
            if(!book.Preferences.Sound)ReminderAudio.Stop();
            if(book.Preferences.Animation)PlayPlannerAnimation();else StopPlannerAnimation();
        }
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
