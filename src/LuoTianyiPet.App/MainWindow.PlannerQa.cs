using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Reflection;
using System.Runtime.InteropServices;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")] private static extern nint PlannerQaForeground();
    private async Task RunPlannerQaAsync()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "PlannerQa", DateTime.UtcNow.Ticks.ToString());
        Directory.CreateDirectory(path);
        List<string> checks = [];
        try
        {
            using ReminderService service = new(new LocalAppPaths(Path.Combine(path, "UserData")));
            await service.LoadAsync();
            await service.ChangeAsync(b =>
            {
                b.Items.Add(new ReminderItem { Calendar = true, Title = "项目会议：核对日历与闹钟", Notes = "准备资料和实验数据。中文长内容换行验证。", Start = DateTime.Today.AddHours(14), Enabled = false });
                b.Items.Add(new ReminderItem { Title = "测试重复闹钟", Repeat = ReminderRepeat.Weekly, Weekdays = [DayOfWeek.Monday, DayOfWeek.Friday], Start = DateTime.Today.AddHours(9) });
                for (int i = 0; i < 5; i++) b.Items.Add(new ReminderItem { Calendar = true, Enabled = false, Title = "当天行程 " + i, Start = DateTime.Today.AddHours(10 + i) });
                b.RestOverrides[DateTime.Today.ToString("yyyy-MM-dd")] = true;
            });
            PlannerWindow window = new(service, false);
            window.Show(); await Task.Delay(300);
            void Capture(string name)
            {
                window.UpdateLayout();
                RenderTargetBitmap bitmap = new((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(path, name + ".png")); encoder.Save(file);
                checks.Add("PASS rendered " + name);
            }
            Capture("month");
            if (Environment.GetCommandLineArgs().Contains("--qa-calendar-holidays"))
            {
                var dateField = typeof(PlannerWindow).GetField("_date", BindingFlags.NonPublic | BindingFlags.Instance)!;
                await service.ChangeAsync(b => b.WeekView = true);
                foreach (var holidayDate in new[] { new DateTime(2026, 6, 21), new DateTime(2026, 11, 26), new DateTime(2026, 12, 25) })
                {
                    dateField.SetValue(window, holidayDate);
                    window.Navigate(false);
                    Capture("holidays-" + holidayDate.Month);
                }
                await service.ChangeAsync(b => b.WeekView = false);
                dateField.SetValue(window, DateTime.Today); window.Navigate(false);
            }
            await service.ChangeAsync(b => b.WeekView = true); Capture("week");
            window.Navigate(true); Capture("alarms");
            typeof(PlannerWindow).GetMethod("Edit", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object?[] { null, false });
            Capture("alarm-editor");
            FrameworkElement Named(DependencyObject parent, string name)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is FrameworkElement element && element.Name == name) return element;
                    try { return Named(child, name); } catch (KeyNotFoundException) { }
                }
                throw new KeyNotFoundException(name);
            }
            ((System.Windows.Controls.TextBox)Named(window, "ReminderTitle")).Text = "界面创建测试";
            ((System.Windows.Controls.ComboBox)Named(window, "ReminderMode")).SelectedIndex = 1;
            window.UpdateLayout();
            var input = (System.Windows.Controls.TextBox)Named(window, "ReminderDuration");
            input.Text = "24:00:01";
            var save = (System.Windows.Controls.Button)Named(window, "SaveReminder");
            save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Task.Delay(150);
            if (service.Book.Items.Count != 7) throw new InvalidOperationException("UI accepted over-24-hour input");
            checks.Add("PASS editor rejects over 24 hours without changing data");
            input.Text = "00:10:00";
            save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Task.Delay(350);
            if (service.Book.Items.Count != 8 || service.Book.LastDurationSeconds != 600) throw new InvalidOperationException("Editor save failed");
            checks.Add("PASS actual editor creates and saves relative alarm and last duration");
            window.Navigate(false);
            typeof(PlannerWindow).GetMethod("EditWorkdays", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            Capture("workdays");
            window.Navigate(false);
            if (window.ActualWidth < 680) throw new InvalidOperationException("Calendar width invalid");
            checks.Add("PASS minimum width and singleton navigation");
            var reopened = await new ReminderStore(new LocalAppPaths(Path.Combine(path, "UserData"))).LoadAsync();
            if (reopened.Items.Count != 8 || !reopened.WeekView) throw new InvalidOperationException("Persistence failure");
            checks.Add("PASS real persistent data roundtrip");
            if (CalendarLabels.Get(new DateTime(2026, 6, 19)) != "端午节") throw new InvalidOperationException("Festival failure");
            checks.Add("PASS offline lunar date");
            DateTime trigger = DateTime.Now.AddSeconds(1);
            await service.ChangeAsync(b => b.Items.Add(new ReminderItem { Title = "真实计时到点测试", Notes = "测试正文：不会写入日志，只保存在隔离测试目录。", Start = trigger, Sound = false, Relative = true, DurationSeconds = 1 }));
            await Task.Delay(1600);
            if (service.Book.Items.Last().PendingAt == null) throw new InvalidOperationException("Real scheduler did not fire");
            checks.Add("PASS real scheduler fires and persists pending reminder");
            _reminders = service;
            _stateMachine.CancelActiveReaction();
            if (PlannerPresentationSafe(new(false, null, false)) || PlannerPresentationSafe(new(true, "test", true)) || PlannerPresentationSafe(new(true, "YuanShen", false)))
                throw new InvalidOperationException("Safety fallback failure");
            checks.Add("PASS unknown foreground, fullscreen and protected game suppress presentation");
            _systemSessionUnavailable = true;
            if (PlannerPresentationSafe(new(true, "test", false))) throw new InvalidOperationException("Lockscreen failure");
            _systemSessionUnavailable = false;
            checks.Add("PASS locked session suppresses presentation");
            nint foregroundBeforeCard = PlannerQaForeground();
            RefreshReminderCardCore(true);
            if (PlannerQaForeground() != foregroundBeforeCard) throw new InvalidOperationException("Reminder stole focus");
            checks.Add("PASS automatic reminder preserves foreground focus");
            if (_reminderCard?.IsVisible != true || _reminderSummary?.Text.Contains("1 项") != true)
                throw new InvalidOperationException("Reminder card missing");
            _reminderCard.UpdateLayout();
            RenderTargetBitmap cardBitmap = new((int)_reminderCard.ActualWidth, (int)_reminderCard.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            cardBitmap.Render(_reminderCard);
            PngBitmapEncoder cardEncoder = new(); cardEncoder.Frames.Add(BitmapFrame.Create(cardBitmap));
            using (var output = File.Create(Path.Combine(path, "due-card.png"))) cardEncoder.Save(output);
            checks.Add("PASS due card shows title, full notes and actions");
            RefreshReminderCardCore(false);
            if (_reminderCard.IsVisible) throw new InvalidOperationException("Unsafe card not hidden");
            checks.Add("PASS safety transition immediately hides card without losing pending data");
            var pending = service.Book.Items.Last();
            await service.ChangeAsync(b => ReminderSchedule.Dismiss(b.Items.Single(i => i.Id == pending.Id), DateTime.Now));
            if (service.Book.Items.Last().PendingAt != null) throw new InvalidOperationException("Dismiss failure");
            checks.Add("PASS dismiss persisted");
            await service.ChangeAsync(b => { b.Items.Clear(); b.WeekView = false; b.Items.Add(new ReminderItem { Calendar = true, Title = "项目会议", Start = DateTime.Now.AddMinutes(29), Sound = false }); });
            window.Navigate(false); Capture("calendar-alarm-icon");
            RefreshReminderCardCore(true);
            if (_reminderCard?.IsVisible != true || _reminderSummary?.Text.Contains("项目会议") != true) throw new InvalidOperationException("Thirty minute countdown missing");
            var toggle = (System.Windows.Controls.CheckBox)Named(window, "CalendarUpcoming");
            toggle.IsChecked = false; toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.CheckBox.ClickEvent)); await Task.Delay(200);
            RefreshReminderCardCore(true);
            if (service.Book.ShowUpcoming || _reminderCard.IsVisible) throw new InvalidOperationException("Countdown switch failed");
            checks.Add("PASS thirty-minute calendar card and global switch persists and hides");
            var body = (ScrollViewer)typeof(PlannerWindow).GetField("_body", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            typeof(PlannerWindow).GetField("_date", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, new DateTime(2026, 8, 1));
            window.Navigate(false); Capture("six-week-month");
            if (body.ScrollableHeight > 1 || ((Grid)body.Content).RowDefinitions.Count != 7) throw new InvalidOperationException("Six-week calendar does not fit");
            checks.Add("PASS all six calendar weeks visible without scrolling");
            typeof(PlannerWindow).GetMethod("Edit", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object?[] { service.Book.Items.Single(), true });
            window.UpdateLayout();
            ((System.Windows.Controls.ComboBox)Named(window, "ReminderRepeat")).SelectedIndex = 3;
            Capture("calendar-dates-editor");
            if (body.ScrollableHeight > 1) throw new InvalidOperationException("Expanded editor does not fit default window");
            ((System.Windows.Controls.CheckBox)Named(window, "CalendarCountdown")).IsChecked = false;
            ((System.Windows.Controls.Button)Named(window, "SaveReminder")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Task.Delay(250);
            if (service.Book.Items.Single().ShowCountdown) throw new InvalidOperationException("Per-event switch not saved");
            checks.Add("PASS expanded editor fits and per-event countdown switch persists");
            _reminderCard.Close(); _reminderCard = null; _reminders = null;
            window.Close();
            File.WriteAllLines(Path.Combine(path, "result.txt"), checks);
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(path, "FAILED.txt"), ex.ToString()); }
        finally { System.Windows.Application.Current.Shutdown(); }
    }
}
