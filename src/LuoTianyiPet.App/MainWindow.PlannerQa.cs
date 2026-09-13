using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;
using Button=System.Windows.Controls.Button;
using CheckBox=System.Windows.Controls.CheckBox;
using TextBox=System.Windows.Controls.TextBox;
namespace LuoTianyiPet.App;
public partial class MainWindow
{
    [DllImport("user32.dll",EntryPoint="GetForegroundWindow")]private static extern nint PlannerQaForeground();
    private async Task RunPlannerQaAsync()
    {
        string path=Path.Combine(AppContext.BaseDirectory,"PlannerQa",DateTime.UtcNow.Ticks.ToString());Directory.CreateDirectory(path);List<string> checks=[];
        try
        {
            using ReminderService service=new(new LocalAppPaths(Path.Combine(path,"UserData")));await service.LoadAsync();
            DateTime day=DateTime.Today.AddDays(1);
            await service.ChangeAsync(b=>{for(int n=0;n<3;n++)b.Items.Add(new ReminderItem{Calendar=true,Title=new[]{"周会","项目讨论","运动"}[n],Notes=new[]{"核对本周计划","准备演示资料，确认下一阶段安排","跑步5公里"}[n],Start=day.AddHours(9+n*4),Enabled=n<2,ReminderCreated=n<2,EarlyEnabled=false});b.Items.Add(new ReminderItem{Title="起床",Start=day.AddHours(8.5),Repeat=ReminderRepeat.Daily,ReminderCreated=true,EarlyEnabled=false});b.Items.Add(new ReminderItem{Title="喝水休息一下",Relative=true,Start=DateTime.Now.AddMinutes(30),DurationSeconds=1800,ReminderCreated=true,EarlyEnabled=false});});
            PlannerWindow window=new(service,false);window.Show();await Task.Delay(150);
            void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);checks.Add("PASS "+name);}
            IEnumerable<DependencyObject> Tree(DependencyObject d){yield return d;for(int n=0;n<VisualTreeHelper.GetChildrenCount(d);n++)foreach(var x in Tree(VisualTreeHelper.GetChild(d,n)))yield return x;}
            FrameworkElement Named(Window w,string name)=>Tree(w).OfType<FrameworkElement>().First(x=>x.Name==name);
            void Click(Window w,string name)=>((Button)Named(w,name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            void Tick(Window w,string name,bool value){var c=(CheckBox)Named(w,name);c.IsChecked=value;c.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));w.UpdateLayout();}
            void Snapshot(Window w,string name){w.UpdateLayout();var bmp=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(w);PngBitmapEncoder png=new();png.Frames.Add(BitmapFrame.Create(bmp));using var f=File.Create(Path.Combine(path,name+".png"));png.Save(f);checks.Add("PASS rendered "+name);}
            void Set(string name,object value)=>typeof(PlannerWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,value);
            void Edit(ReminderItem? i,bool calendar){typeof(PlannerWindow).GetMethod("Edit",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,new object?[]{i,calendar});window.UpdateLayout();}
            Set("_date",day);window.Navigate(false);Snapshot(window,"01-month");
            var body=(ScrollViewer)typeof(PlannerWindow).GetField("_body",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window)!;
            Check(((Grid)body.Content).ColumnDefinitions.Count==2,"A02 side panel with undimmed month");
            Click(window,"Day"+day.ToString("yyyyMMdd"));window.UpdateLayout();Check(((Grid)body.Content).ColumnDefinitions.Count==1,"A02 same date collapses panel");
            Click(window,"Day"+day.ToString("yyyyMMdd"));window.UpdateLayout();Click(window,"SetWorkdays");Snapshot(window,"02-workdays");Click(window,"SetWorkdays");
            await service.ChangeAsync(b=>b.WeekView=true);Snapshot(window,"03-week");await service.ChangeAsync(b=>b.WeekView=false);
            Edit(null,true);Snapshot(window,"04-new-event");Check(((CheckBox)Named(window,"CreateAlarm")).IsChecked==false,"A06 new calendar reminder disabled");
            Check(!Tree(window).OfType<FrameworkElement>().Any(e=>e.Name=="EarlyReminder"),"A06 early row absent while disabled");
            var dateControl=(DatePicker)Named(window,"ReminderDate");dateControl.ApplyTemplate();
            ((Button)dateControl.Template.FindName("PART_Button",dateControl)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);
            var datePopup=(System.Windows.Controls.Primitives.Popup)dateControl.Template.FindName("PART_Popup",dateControl);
            Check(dateControl.IsDropDownOpen&&datePopup.IsOpen&&datePopup.Child!=null,"styled date picker opens a real calendar");dateControl.IsDropDownOpen=false;
            Tick(window,"CreateAlarm",true);Check(((CheckBox)Named(window,"EarlyReminder")).IsChecked==false,"A07 early starts disabled");Tick(window,"EarlyReminder",true);Check(((TextBox)Named(window,"EarlyMinutes")).Text=="30","A07 thirty minute initial value");
            Tick(window,"NoTime",true);Check(((CheckBox)Named(window,"CreateAlarm")).IsChecked==false&&!((CheckBox)Named(window,"CreateAlarm")).IsEnabled,"A29 unspecified time disables reminder");
            ((TextBox)Named(window,"ReminderTitle")).Text="不指定时间事项";Click(window,"SaveReminder");await Task.Delay(250);Check(service.Book.Items.Last().HasTime==false&&!service.Book.Items.Last().Enabled,"A29 unspecified time persists without midnight alarm");
            var eventItem=service.Book.Items.First();Edit(eventItem,true);Snapshot(window,"05-edit-event");Check(((TextBox)Named(window,"ReminderNotes")).AcceptsReturn==false,"A09 single line note");
            Tick(window,"EarlyReminder",true);((TextBox)Named(window,"EarlyMinutes")).Text="45";Click(window,"SaveReminder");await Task.Delay(250);Check(service.Book.Items.First(i=>i.Id==eventItem.Id).EarlyMinutes==45,"A10 early duration persists");
            window.Navigate(true);Snapshot(window,"06-alarms");Edit(null,false);Snapshot(window,"07-new-alarm");
            var countdownButton=Tree(window).OfType<Button>().First(b=>Equals(b.Content,"⌛ 倒计时"));countdownButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();Snapshot(window,"08-new-countdown");
            void Digits(string h,string m,string s){((TextBox)Named(window,"CountdownHours")).Text=h;((TextBox)Named(window,"CountdownMinutes")).Text=m;((TextBox)Named(window,"CountdownSeconds")).Text=s;}
            foreach(var preset in new[]{("5分钟",300),("15分钟",900),("30分钟",1800),("1小时",3600)}){Tree(window).OfType<Button>().First(b=>Equals(b.Content,preset.Item1)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));int h=int.Parse(((TextBox)Named(window,"CountdownHours")).Text),m=int.Parse(((TextBox)Named(window,"CountdownMinutes")).Text),sec=int.Parse(((TextBox)Named(window,"CountdownSeconds")).Text);Check(h*3600+m*60+sec==preset.Item2,"A16 preset "+preset.Item1);}
            foreach(string part in new[]{"CountdownHours","CountdownMinutes","CountdownSeconds"}){var input=(TextBox)Named(window,part);input.Text="01";input.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,120){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseWheelEvent});Check(input.Text=="02","A16 wheel "+part);}
            int count=service.Book.Items.Count;Digits("24","00","01");Click(window,"SaveReminder");await Task.Delay(100);Check(service.Book.Items.Count==count,"A17 rejects 24:00:01");Digits("0","0","0");Click(window,"SaveReminder");await Task.Delay(100);Check(service.Book.Items.Count==count,"A17 rejects zero");Digits("24","0","0");Click(window,"SaveReminder");await Task.Delay(250);Check(service.Book.Items.Count==count+1&&service.Book.Items.Last().DurationSeconds==86400,"A17 accepts 24 hours with optional empty name");
            var timer=service.Book.Items.Last();await service.ChangeAsync(b=>ReminderEngine.Pause(b,timer.Id,DateTime.Now));var frozen=service.Book.Items.Last().PausedSeconds;await Task.Delay(100);Check(frozen==service.Book.Items.Last().PausedSeconds,"A18 pause freezes remaining");await service.ChangeAsync(b=>ReminderEngine.Resume(b,timer.Id,DateTime.Now));Check(service.Book.Items.Last().PausedSeconds==null,"A18 resume");
            DateTime at=DateTime.Now.AddMinutes(20);await service.ChangeAsync(b=>{b.Items.Clear();b.Occurrences.Clear();for(int n=0;n<2;n++){var i=new ReminderItem{Calendar=true,Title="测试事项"+n,Notes="准备演示资料",Start=at.AddMinutes(n),ReminderCreated=true,EarlyEnabled=true,CheckedThrough=DateTime.Now};b.Items.Add(i);b.Occurrences.Add(new(){RuleId=i.Id,At=i.Start,Phase=ReminderPhase.Early});}});
            _reminders=service;var foreground=PlannerQaForeground();RefreshReminderCardCore(true);Check(PlannerQaForeground()==foreground,"reminder does not steal focus");Snapshot(_reminderCard!,"09-early-reminder");
            Click(_reminderCard!,"AcknowledgeReminder");await Task.Delay(200);RefreshReminderCardCore(true);Check(service.Book.Occurrences.Count(o=>o.Phase==ReminderPhase.AcknowledgedEarly)==1,"A19 acknowledge one early retains due");
            Click(_reminderCard!,"AcknowledgeReminder");await Task.Delay(200);RefreshReminderCardCore(true);Snapshot(_reminderCard!,"10-capsule");Check(service.Book.Occurrences.All(o=>o.Phase==ReminderPhase.AcknowledgedEarly),"A22 both instances combined");
            _reminderExpandedUntil=DateTime.Now.AddSeconds(30);_reminderCardKey="";RefreshReminderCardCore(true);Snapshot(_reminderCard!,"11-capsule-expanded");Click(_reminderCard!,"CancelOccurrence");await Task.Delay(200);Check(service.Book.Occurrences.Count(o=>o.Phase==ReminderPhase.Cancelled)==1,"A21 cancels only selected instance");
            await service.ChangeAsync(b=>ReminderEngine.Advance(b,at.AddMinutes(2)));RefreshReminderCardCore(true);Snapshot(_reminderCard!,"12-due-reminder");Check(!Tree(_reminderCard!).OfType<Button>().Any(b=>b.Name=="CancelOccurrence"),"A24 due only acknowledge and snooze");
            RefreshReminderCardCore(false);Check(!_reminderCard!.IsVisible,"safety hides reminder without dropping data");
            Check(!PlannerPresentationSafe(new(false,null,false))&&!PlannerPresentationSafe(new(true,"YuanShen",false))&&!PlannerPresentationSafe(new(true,"app",true)),"unknown foreground game fullscreen suppress reminders");
            var stored=await new ReminderStore(new LocalAppPaths(Path.Combine(path,"UserData"))).LoadAsync();Check(stored.Occurrences.Any(o=>o.Phase==ReminderPhase.Cancelled),"A27 cancellation survives reload");
            using(var verify=new ReminderService(new LocalAppPaths(Path.Combine(path,"UserData")))){await verify.LoadAsync();Check(verify.Book.Occurrences.Any(o=>o.Phase==ReminderPhase.Cancelled),"A27 service reload preserves cancellation");}
            // Scope actions operate on the original group ID, never on copied date records.
            await service.ChangeAsync(b=>{b.Items.Clear();b.Occurrences.Clear();b.Items.Add(new ReminderItem{Calendar=true,Title="多日期事项",Start=day.AddHours(14),Dates=[day,day.AddDays(7)],Repeat=ReminderRepeat.Dates,ReminderCreated=true,EarlyEnabled=false});});
            window.Navigate(false);Set("_occurrenceDate",day);Edit(service.Book.Items.Single(),true);Click(window,"DeleteOccurrence");window.UpdateLayout();Click(window,"ConfirmGroupDelete");await Task.Delay(200);Check(service.Book.Items.Single().Dates.Count==1,"scope delete keeps other date");
            Set("_manage",true);typeof(PlannerWindow).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance,null,Type.EmptyTypes,null)!.Invoke(window,null);
            var chosen=(HashSet<Guid>)typeof(PlannerWindow).GetField("_selectedGroups",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window)!;chosen.Add(service.Book.Items.Single().Id);typeof(PlannerWindow).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance,null,Type.EmptyTypes,null)!.Invoke(window,null);Click(window,"DeleteSelectedGroups");window.UpdateLayout();Click(window,"ConfirmGroupDelete");await Task.Delay(200);Check(service.Book.Items.Count==0,"bulk group deletion persists");
            DateSelectionWindow picker=new([day],day){Owner=window};picker.Show();picker.UpdateLayout();Click(picker,"Date"+day.AddDays(1).ToString("yyyyMMdd"));Check(picker.Selection.Count==2,"A13 direct date multiselect");Snapshot(picker,"14-date-picker");picker.Close();
            window.Navigate(false);window.Width=900;Set("_date",new DateTime(2026,8,1));Snapshot(window,"15-small-six-week-month");Check(body.ScrollableWidth==0&&body.ScrollableHeight==0,"small six week calendar fits");
            SettingsWindow settings=new(new(),new(),new(),new(),new(),false,null,service.Book.Preferences);settings.NavigateNotifications();settings.Show();await Task.Delay(200);settings.NotificationPage.ScrollToEnd();settings.UpdateLayout();Snapshot(settings,"13-reminder-settings");Check(settings.AlarmAnimationCheckBox.IsVisible&&settings.AlarmSoundCheckBox.IsVisible,"alarm settings integrated below notifications");settings.Close();
            await service.ChangeAsync(b=>{b.Items.Clear();b.Occurrences.Clear();b.Preferences=new(){Sound=false,Animation=true};var i=new ReminderItem{Title="来啦闹钟验证",Start=DateTime.Now,CheckedThrough=DateTime.Now.AddSeconds(-1)};b.Items.Add(i);ReminderEngine.Advance(b,DateTime.Now);});
            bool originalTopmost=Topmost;RefreshReminderCardCore(true);await Task.Delay(180);
            Check(Topmost&&_plannerAlarmTopmost!=null,"alarm temporarily forces pet topmost");
            Check(_plannerAlarmReaction!=null&&_stateMachine.ActiveReactionToken==_plannerAlarmReaction,"original coming animation owns alarm reaction");
            Snapshot(this,"16-original-alarm-animation");
            await service.ChangeAsync(b=>{var o=b.Occurrences.Single();o.RoundStartedAt=DateTime.Now.AddSeconds(-223);ReminderEngine.Advance(b,DateTime.Now);});RefreshReminderCardCore(true);
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.DueSnoozed,"song timeout enters ten minute retry");
            Check(_plannerAlarmReaction==null&&_plannerAlarmTopmost==null&&Topmost==originalTopmost,"timeout restores prior topmost and animation");
            Check(!ReminderAudio.IsPlaying,"timeout stops reminder audio");
            await service.ChangeAsync(b=>{var o=b.Occurrences.Single();o.SnoozeAt=DateTime.Now;ReminderEngine.Advance(b,DateTime.Now);});RefreshReminderCardCore(true);
            Check(_plannerAlarmReaction!=null&&Topmost,"retry resumes original animation and topmost");
            Click(_reminderCard!,"AcknowledgeReminder");await Task.Delay(200);RefreshReminderCardCore(true);
            Check(service.Book.Occurrences.Single().Phase==ReminderPhase.Done&&_plannerAlarmReaction==null&&Topmost==originalTopmost&&!ReminderAudio.IsPlaying,"closing due reminder immediately releases presentation and future retries");
            Check(File.Exists(ReminderAudio.SongPath),"user supplied MP3 packaged locally");
            MediaPlayer media=new(){Volume=0};TaskCompletionSource<bool> opened=new();media.MediaOpened+=(_,_)=>opened.TrySetResult(true);media.MediaFailed+=(_,_)=>opened.TrySetResult(false);media.Open(new Uri(ReminderAudio.SongPath));await Task.WhenAny(opened.Task,Task.Delay(5000));
            Check(media.NaturalDuration.HasTimeSpan&&media.NaturalDuration.TimeSpan.TotalSeconds>220&&media.NaturalDuration.TimeSpan.TotalSeconds<225,"song duration matches three minutes forty two seconds");media.Close();
            window.Close();_reminderCard.Close();_reminderCard=null;_reminders=null;File.WriteAllLines(Path.Combine(path,"result.txt"),checks);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(path,"FAILED.txt"),ex.ToString());}
        finally{System.Windows.Application.Current.Shutdown();}
    }
}
