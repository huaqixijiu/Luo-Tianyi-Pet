using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunMessageDetailsQaAsync()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "MessageDetailsQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool success, string description)
        {
            if (!success) throw new InvalidOperationException(description);
            checks.Add("PASS " + description);
        }
        try
        {
            await Task.Delay(800);
            _stateMachine.CancelActiveReaction();
            SetMusicIslandsVisible(true);
            var message = new MessageNotificationSummary(MessageProvider.Qq, DateTimeOffset.Now,
                "测试好友、测试群", UnreadCount: 6, MessagePreview: "这是一条测试消息预览，较长的文字自动换行并在两行内收起，不影响人物和侧边定位。", NewNotificationCount: 3);
            await BeginMessageNotificationAsync(message);
            Check(_messageBubble?.IsVisible == true, "Message card is shown in its own surface");
            _messageBubble!.Hide();
            nint foreground = NotificationQaGetForegroundWindow();
            PositionMessageNotification();
            Check(NotificationQaGetForegroundWindow() == foreground, "Synchronous card show preserves foreground focus");
            Check(!IsMessageNotificationDisplaySafe(new(false, null, false)) &&
                !IsMessageNotificationDisplaySafe(new(true, "other", true)),
                "Unknown foreground and fullscreen fail closed for notifications");
            Check(_messageBubble.MessagePreviewText.Visibility == Visibility.Visible &&
                _messageBubble.MessageSourceText.Text == "QQ · 新增 3 条通知", "Preview and new-notification count are visible together");
            Guid? originalToken = _messageNotificationReactionToken;
            Check(TryEnrichActiveMessage(message with { UnreadCount = 7 }, false, true) &&
                _messageNotificationReactionToken == originalToken && _displayedMessageSummary!.UnreadCount == 7,
                "Richer same-source details update the existing card without replaying the reaction");
            ShowMessageNotification(message);
            DesktopRectangle work = GetQuickActionsWorkArea();
            foreach (int scale in new[] { 50, 100, 200 })
            {
                SetDisplayScalePercent(scale, false);
                Left = work.Left + 450; Top = work.Top + 200;
                PositionMessageNotification();
                Check(PetDirectionTransform.ScaleX == 1, $"Scale {scale}: centered character faces right");
                Check(_messageBubble!.Left > Left, $"Scale {scale}: card sits on the character right");
                var before = new Point(Left, Top);
                ShowTrackInfo(new MediaTrackSnapshot(true, true, "测试歌曲", "测试歌手"), true);
                OnRootMouseEnter(this, new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0));
                await Task.Delay(220);
                Check(new Point(Left, Top) == before, $"Scale {scale}: music refresh does not move the character");
                CaptureMessageQa(directory, $"right-{scale}.png");
                Left = work.Right - Width;
                PositionMessageNotification();
                Check(PetDirectionTransform.ScaleX == -1, $"Scale {scale}: right edge mirrors character");
                Check(_messageBubble.Left < Left, $"Scale {scale}: right edge puts the card to the left");
                Check(_messageBubble.Left >= work.Left && _messageBubble.Top >= work.Top &&
                    _messageBubble.Left + _messageBubble.ActualWidth <= work.Right + 1 &&
                    _messageBubble.Top + _messageBubble.ActualHeight <= work.Bottom + 1,
                    $"Scale {scale}: card fits the work area");
                Check(_messageBubble.MessageConversationText.Text == "测试好友、测试群",
                    $"Scale {scale}: mirrored character does not mirror or lose text");
                CaptureMessageQa(directory, $"left-{scale}.png");
            }
            Guid? active = _messageNotificationReactionToken;
            await BeginMessageNotificationAsync(new(MessageProvider.WeChat, DateTimeOffset.Now));
            Check(_messageNotificationReactionToken == active && _messageNotificationCoordinator.HasPending,
                "Second source queues without creating a second card or replacing active token");
            ApplyMessageNotificationPreferences(_settings.Notifications with { EnableQqDetailedReminders = false });
            Check(_messageBubble!.MessageConversationText.Text == "有新消息" &&
                _displayedMessageSummary!.UnreadCount is null && _displayedMessageSummary.MessagePreview is null &&
                _displayedMessageSummary.NewNotificationCount is null && _messageBubble.MessagePreviewText.Text.Length == 0 &&
                _messageBubble.Width == 212,
                "Disabling QQ details removes nickname, preview, both counts and restores compact layout");
            Check(!TryEnrichActiveMessage(message, false, true),
                "Late detailed event cannot restore QQ details after disable");
            var wechat = new MessageNotificationSummary(MessageProvider.WeChat, DateTimeOffset.Now,
                "测试微信好友", MessagePreview: "微信会话摘要测试", NewNotificationCount: 2);
            ShowMessageNotification(wechat);
            Check(_messageBubble.MessageConversationText.Text == "测试微信好友" &&
                _messageBubble.MessagePreviewText.Text == "微信会话摘要测试" &&
                _messageBubble.MessageSourceText.Text == "微信 · 新增 2 次提醒", "WeChat unified details use the shared card with a distinct reminder-count label");
            CaptureMessageQa(directory, "wechat.png");
            ApplyMessageNotificationPreferences(_settings.Notifications with { EnableWeChatDetailedReminders = false });
            Check(_displayedMessageSummary!.ConversationDisplayName is null && _displayedMessageSummary.MessagePreview is null &&
                _displayedMessageSummary.NewNotificationCount is null && _messageBubble.MessagePreviewText.Text.Length == 0,
                "WeChat disable clears nickname preview and count together");
            ApplyMessageNotificationPreferences(_settings.Notifications with { EnableMessageReminders = false });
            Check(!_messageBubble.IsVisible && !_messageNotificationCoordinator.HasPending,
                "Disabling all reminders clears the card and pending details");
            Check(_displayedMessageSummary is null && _messageBubble.MessageConversationText.Text.Length == 0 &&
                System.Windows.Automation.AutomationProperties.GetName(_messageBubble.MessageNotificationBubble) == "聊天消息提醒",
                "Clearing a reminder releases text and accessibility metadata");
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                SettingsWindow? first = _settingsWindow;
                Check(first is not null, "Settings are registered before the modal loop");
                first!.QqDetailedReminderCheckBox.IsChecked = false;
                first.WindowState = WindowState.Minimized;
                ShowSettingsDialog();
                Check(ReferenceEquals(first, _settingsWindow) && first.WindowState == WindowState.Normal &&
                    Application.Current.Windows.OfType<SettingsWindow>().Count() == 1,
                    "Repeated settings request restores the single existing window");
                Check(first.QqDetailedReminderCheckBox.IsChecked == false,
                    "Repeated settings request preserves unsaved edits");
                first.NotificationNavigationRadioButton.IsChecked = true;
                CaptureQuickActionsQa(first, Path.Combine(directory,"settings.png"));
                first.Close();
            }));
            ShowSettingsDialog();
            Check(_settingsWindow is null, "Closing settings releases the singleton reference");
            if (Environment.GetCommandLineArgs().Contains("--qa-live-qq-details"))
            {
                QqTrayDetails? live = await Task.Run(QqTrayDetailsReader.TryRead);
                checks.Add($"LIVE QQ HasDetails={live is not null}; TitleLength={live?.DisplayName.Length ?? 0}; Count={live?.UnreadCount}");
            }
            File.WriteAllLines(Path.Combine(directory,"result.txt"),checks);
            Close();
        }
        catch (Exception exception)
        {
            checks.Add("FAIL " + exception);
            File.WriteAllLines(Path.Combine(directory,"result.txt"),checks);
            Application.Current.Shutdown(1);
        }
    }

    private void CaptureMessageQa(string directory, string name)
    {
        UpdateLayout();
        _messageBubble!.UpdateLayout();
        double x = Math.Min(Left, _messageBubble.Left), y = Math.Min(Top, _messageBubble.Top);
        double width = Math.Max(Left + ActualWidth, _messageBubble.Left + _messageBubble.ActualWidth) - x;
        double height = Math.Max(Top + ActualHeight, _messageBubble.Top + _messageBubble.ActualHeight) - y;
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x0B,0x54,0x86)),null,new Rect(0,0,width,height));
            context.DrawImage(RenderWindow(this),new Rect(Left-x,Top-y,ActualWidth,ActualHeight));
            context.DrawImage(RenderWindow(_messageBubble),
                new Rect(_messageBubble.Left-x,_messageBubble.Top-y,_messageBubble.ActualWidth,_messageBubble.ActualHeight));
        }
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(width),(int)Math.Ceiling(height),96,96,PixelFormats.Pbgra32);
        bitmap.Render(visual);
        PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream output = File.Create(Path.Combine(directory,name)); encoder.Save(output);
    }

    private static RenderTargetBitmap RenderWindow(Window window)
    {
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(window.ActualWidth),
            (int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);
        bitmap.Render((Visual)window.Content);
        return bitmap;
    }

    [DllImport("user32.dll",EntryPoint="GetForegroundWindow")]
    private static extern nint NotificationQaGetForegroundWindow();
}
