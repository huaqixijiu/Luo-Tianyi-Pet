using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class SettingsWindow
{
    // Isolated UI-only QA: no platform source, user configuration, or notification requests.
    internal static async Task<int> RunNotificationSettingsQaAsync()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "NotificationSettingsQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            checks.Add("PASS " + label);
        }
        SettingsWindow? window = null;
        SettingsWindow Create(IMessageNotificationSource? source) => new(
            new MessageNotificationPreferences { EnableMessageReminders = true, EnableQqDetailedReminders = true,
                EnableWeChatDetailedReminders = true, WindowsNotificationAccessGranted = false },
            new WindowPreferences(), new FileTreatPreferences(), new AppearancePreferences(), new MediaPreferences(), false, source);
        try
        {
            foreach (MessageNotificationAccessStatus status in Enum.GetValues(typeof(MessageNotificationAccessStatus)))
            {
                var source = new NotificationSettingsQaSource { Status = status };
                window = Create(source);
                window.Show();
                window.NotificationNavigationRadioButton.IsChecked = true;
                await Task.Delay(180);
                window.UpdateLayout();
                string prefix = status + ": ";
                Check(source.RequestCount == 0, prefix + "Opening settings never requests system permission");
                Check(!window.NotificationRulesExpander.IsExpanded, prefix + "Rules start collapsed");
                Check(window.NotificationPage.ScrollableHeight < 1, prefix + "Collapsed page fits without vertical scroll");
                Check(window.NotificationPage.ScrollableWidth < 1, prefix + "Page never scrolls horizontally");
                Check(window.NotificationAccessStatusText.Text == (status switch {
                    MessageNotificationAccessStatus.Allowed => "已授权", MessageNotificationAccessStatus.Denied => "已拒绝",
                    MessageNotificationAccessStatus.Unspecified => "未授权", MessageNotificationAccessStatus.PackageIdentityRequired => "需安装版", _ => "暂不可用" }), prefix + "Actual source status has a compact label");
                Check(window.NotificationAccessButton.IsEnabled == (status is MessageNotificationAccessStatus.Unspecified or MessageNotificationAccessStatus.Unavailable), prefix + "Only actionable permission states enable the button");
                Check(window.NotificationQqIcon.Source is BitmapImage qq && qq.UriSource.ToString().EndsWith("notification-qq.png") &&
                    window.NotificationWeChatIcon.Source is BitmapImage wc && wc.UriSource.ToString().EndsWith("notification-wechat.png"), prefix + "Both icons use the existing user-derived PNG assets");
                CaptureNotificationSettings(window, Path.Combine(directory, status + ".png"), 1);
                window.MessageReminderCheckBox.Focus();
                window.MessageReminderCheckBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                Check(window.QqDetailedReminderCheckBox.IsKeyboardFocused, prefix + "Tab reaches QQ");
                window.QqDetailedReminderCheckBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                Check(window.WeChatDetailedReminderCheckBox.IsKeyboardFocused, prefix + "Tab reaches WeChat");
                ((IToggleProvider)new ToggleButtonAutomationPeer(window.QqDetailedReminderCheckBox)).Toggle();
                Check(window.QqDetailedReminderCheckBox.IsChecked == false && window.WeChatDetailedReminderCheckBox.IsChecked == true,
                    prefix + "QQ and WeChat detail choices remain independent");
                window.MessageReminderCheckBox.IsChecked = false;
                Check(window.WeChatDetailedReminderCheckBox.IsChecked == true, prefix + "Master off preserves detail preference");
                window.MessageReminderCheckBox.IsChecked = true;
                window.QqDetailedReminderCheckBox.IsChecked = true;
                Check(window.SaveStatusText.Text == "有未保存的更改", prefix + "Changes are marked unsaved");
                CaptureNotificationSettings(window, Path.Combine(directory, status + ".png"), 1);
                ToggleButton disclosure = FindNotificationVisual<ToggleButton>(window.NotificationRulesExpander)!;
                ((IToggleProvider)new ToggleButtonAutomationPeer(disclosure)).Toggle();
                window.UpdateLayout();
                Check(window.NotificationRulesExpander.IsExpanded && window.NotificationPage.ScrollableHeight > 0,
                    prefix + "Disclosure opens into a scrollable explanation");
                window.NotificationPage.ScrollToEnd();
                window.UpdateLayout();
                Check(window.SaveStatusText.IsVisible, prefix + "Footer remains visible while reading rules");
                if (status == MessageNotificationAccessStatus.Allowed)
                    CaptureNotificationSettings(window, Path.Combine(directory, "rules-expanded.png"), 1);
                ((IToggleProvider)new ToggleButtonAutomationPeer(disclosure)).Toggle();
                window.UpdateLayout();
                Check(!window.NotificationRulesExpander.IsExpanded && window.NotificationPage.ScrollableHeight < 1,
                    prefix + "Closing rules restores the compact page");
                if (status == MessageNotificationAccessStatus.Unspecified)
                {
                    source.Status = MessageNotificationAccessStatus.Allowed;
                    window.NotificationAccessButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    await Task.Delay(30);
                    Check(source.RequestCount == 1 && window.SelectedNotificationPreferences.WindowsNotificationAccessGranted,
                        "Grant button updates the pending preference exactly once");
                    Check(window.NotificationAccessStatusText.Text == "已授权" && !window.NotificationAccessButton.IsVisible,
                        "Granted state removes the redundant authorization button");
                }
                if (status == MessageNotificationAccessStatus.Allowed)
                {
                    Check(window.SelectedNotificationPreferences.WindowsNotificationAccessGranted,
                        "Existing system grant is preserved when saving stale local preferences");
                    CaptureNotificationSettings(window, Path.Combine(directory, "allowed-125.png"), 1.25);
                    CaptureNotificationSettings(window, Path.Combine(directory, "allowed-150.png"), 1.5);
                }
                window.Close(); window = null;
            }
            window = Create(null);
            window.Show(); window.NotificationNavigationRadioButton.IsChecked = true;
            Check(!window.NotificationAccessButton.IsEnabled && window.NotificationAccessStatusText.Text == "暂不可用",
                "Absent platform source has no dead authorization action");
            window.Close(); window = Create(new NotificationSettingsQaSource { Status = MessageNotificationAccessStatus.Allowed });
            SettingsWindow modal = window;
            _ = modal.Dispatcher.BeginInvoke(new Action(() =>
            {
                modal.MessageReminderCheckBox.IsChecked = false;
                modal.QqDetailedReminderCheckBox.IsChecked = false;
                modal.WeChatDetailedReminderCheckBox.IsChecked = true;
                modal.OnSaveClick(modal, new RoutedEventArgs());
            }));
            Check(modal.ShowDialog() == true, "Save accepts the settings dialog");
            Check(!modal.SelectedNotificationPreferences.EnableMessageReminders && !modal.SelectedNotificationPreferences.EnableQqDetailedReminders &&
                modal.SelectedNotificationPreferences.EnableWeChatDetailedReminders && modal.SelectedNotificationPreferences.WindowsNotificationAccessGranted,
                "Save commits all notification choices and the current grant");
            window = null;
        }
        catch (Exception error) { checks.Add("FAIL " + error); }
        finally { window?.Close(); }
        File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
        return checks.Any(line => line.StartsWith("FAIL", StringComparison.Ordinal)) ? 1 : 0;
    }

    private static T? FindNotificationVisual<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindNotificationVisual<T>(child) is T nested) return nested;
        }
        return null;
    }

    private static void CaptureNotificationSettings(SettingsWindow window, string path, double scale)
    {
        FrameworkElement content = (FrameworkElement)window.Content;
        window.UpdateLayout();
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(content.ActualWidth * scale),
            (int)Math.Ceiling(content.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(content);
        PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream file = File.Create(path); encoder.Save(file);
    }

    private sealed class NotificationSettingsQaSource : IMessageNotificationSource
    {
        public MessageNotificationAccessStatus Status { get; set; }
        public int RequestCount { get; private set; }
        public event EventHandler<MessageNotificationReceivedEventArgs>? NotificationReceived { add { } remove { } }
        public MessageNotificationAccessStatus GetAccessStatus() => Status;
        public ValueTask<MessageNotificationAccessStatus> RequestAccessAsync() { RequestCount++; return new(Status); }
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
