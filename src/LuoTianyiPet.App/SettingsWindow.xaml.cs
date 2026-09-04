using System.Windows;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class SettingsWindow : Window
{
    private readonly IMessageNotificationSource? _messageNotificationSource;

    public SettingsWindow(
        MessageNotificationPreferences notificationPreferences,
        WindowPreferences windowPreferences,
        FileTreatPreferences fileTreatPreferences,
        bool startupRegistrationEnabled,
        IMessageNotificationSource? messageNotificationSource)
    {
        ArgumentNullException.ThrowIfNull(notificationPreferences);
        ArgumentNullException.ThrowIfNull(windowPreferences);
        ArgumentNullException.ThrowIfNull(fileTreatPreferences);
        SelectedNotificationPreferences = notificationPreferences;
        SelectedWindowPreferences = windowPreferences;
        SelectedFileTreatPreferences = fileTreatPreferences;
        StartWithWindowsSelected = startupRegistrationEnabled;
        _messageNotificationSource = messageNotificationSource;
        InitializeComponent();

        MessageReminderCheckBox.IsChecked = notificationPreferences.EnableMessageReminders;
        StartWithWindowsCheckBox.IsChecked = startupRegistrationEnabled;
        AlwaysOnTopCheckBox.IsChecked = windowPreferences.AlwaysOnTop;
        DesktopFileTreatsCheckBox.IsChecked = fileTreatPreferences.EnableDesktopFileTreats;
    }

    public MessageNotificationPreferences SelectedNotificationPreferences { get; private set; }

    public WindowPreferences SelectedWindowPreferences { get; private set; }

    public FileTreatPreferences SelectedFileTreatPreferences { get; private set; }

    public bool StartWithWindowsSelected { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateNotificationAccessDisplay();
    }

    private async void OnRequestNotificationAccessClick(object sender, RoutedEventArgs e)
    {
        if (_messageNotificationSource is null)
        {
            UpdateNotificationAccessDisplay();
            return;
        }

        NotificationAccessButton.IsEnabled = false;
        MessageNotificationAccessStatus status =
            await _messageNotificationSource.RequestAccessAsync();
        SelectedNotificationPreferences = SelectedNotificationPreferences with
        {
            WindowsNotificationAccessGranted =
                status == MessageNotificationAccessStatus.Allowed,
        };
        UpdateNotificationAccessDisplay(status);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SelectedNotificationPreferences = SelectedNotificationPreferences with
        {
            EnableMessageReminders = MessageReminderCheckBox.IsChecked == true,
        };
        StartWithWindowsSelected = StartWithWindowsCheckBox.IsChecked == true;
        SelectedWindowPreferences = SelectedWindowPreferences with
        {
            AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true,
            StartWithWindows = StartWithWindowsSelected,
        };
        SelectedFileTreatPreferences = SelectedFileTreatPreferences with
        {
            EnableDesktopFileTreats = DesktopFileTreatsCheckBox.IsChecked == true,
        };
        DialogResult = true;
    }

    private void UpdateNotificationAccessDisplay(MessageNotificationAccessStatus? knownStatus = null)
    {
        MessageNotificationAccessStatus status = knownStatus ??
            (SelectedNotificationPreferences.WindowsNotificationAccessGranted
                ? _messageNotificationSource?.GetAccessStatus() ??
                    MessageNotificationAccessStatus.Unavailable
                : MessageNotificationAccessStatus.Unspecified);
        NotificationAccessStatusText.Text = status switch
        {
            MessageNotificationAccessStatus.Allowed =>
                "Windows 已授权；桌宠只读取通知来源，不读取正文。",
            MessageNotificationAccessStatus.Unspecified =>
                "尚未授权。点击后由 Windows 显示系统权限对话框。",
            MessageNotificationAccessStatus.Denied =>
                "Windows 已拒绝访问；需要在系统隐私设置中手动允许。",
            MessageNotificationAccessStatus.PackageIdentityRequired =>
                "当前免安装版没有 MSIX 包身份；安装后续 MSIX 测试包后才能授权。",
            _ => "当前系统暂时无法提供通知访问；其它桌宠功能不受影响。",
        };
        NotificationAccessButton.IsEnabled =
            status is MessageNotificationAccessStatus.Unspecified or
                MessageNotificationAccessStatus.Unavailable;
        NotificationAccessButton.Content = status == MessageNotificationAccessStatus.Allowed
            ? "已授权"
            : "授权访问";
    }

}
