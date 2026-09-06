using System.Windows;
using System.Windows.Controls;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class SettingsWindow : Window
{
    private readonly IMessageNotificationSource? _messageNotificationSource;
    public SettingsWindow(
        MessageNotificationPreferences notificationPreferences,
        WindowPreferences windowPreferences,
        FileTreatPreferences fileTreatPreferences,
        AppearancePreferences appearancePreferences,
        MediaPreferences mediaPreferences,
        bool startupRegistrationEnabled,
        IMessageNotificationSource? messageNotificationSource)
    {
        ArgumentNullException.ThrowIfNull(notificationPreferences);
        ArgumentNullException.ThrowIfNull(windowPreferences);
        ArgumentNullException.ThrowIfNull(fileTreatPreferences);
        ArgumentNullException.ThrowIfNull(appearancePreferences);
        ArgumentNullException.ThrowIfNull(mediaPreferences);
        SelectedNotificationPreferences = notificationPreferences;
        SelectedWindowPreferences = windowPreferences;
        SelectedFileTreatPreferences = fileTreatPreferences;
        SelectedAppearancePreferences = AppearancePreferences.Normalize(appearancePreferences);
        SelectedMediaPreferences = MediaPreferences.Normalize(mediaPreferences);
        StartWithWindowsSelected = startupRegistrationEnabled;
        _messageNotificationSource = messageNotificationSource;
        InitializeComponent();
        LoadMusicAnimationOptions();

        MessageReminderCheckBox.IsChecked = notificationPreferences.EnableMessageReminders;
        StartWithWindowsCheckBox.IsChecked = startupRegistrationEnabled;
        AlwaysOnTopCheckBox.IsChecked = windowPreferences.AlwaysOnTop;
        FullBodyStyleCyclingCheckBox.IsChecked =
            SelectedAppearancePreferences.EnableFullBodyStyleCycling;
        DesktopFileTreatsCheckBox.IsChecked = fileTreatPreferences.EnableDesktopFileTreats;
    }

    public MessageNotificationPreferences SelectedNotificationPreferences { get; private set; }

    public WindowPreferences SelectedWindowPreferences { get; private set; }

    public FileTreatPreferences SelectedFileTreatPreferences { get; private set; }

    public AppearancePreferences SelectedAppearancePreferences { get; private set; }

    public MediaPreferences SelectedMediaPreferences { get; private set; }

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
        SelectedAppearancePreferences = AppearancePreferences.Normalize(
            SelectedAppearancePreferences with
            {
                EnableFullBodyStyleCycling = FullBodyStyleCyclingCheckBox.IsChecked == true,
            });
        if (MusicAnimationSelectionComboBox.SelectedItem is ComboBoxItem selectedMusicAnimation &&
            selectedMusicAnimation.Tag is string selection)
        {
            SelectedMediaPreferences = MediaPreferences.Normalize(
                SelectedMediaPreferences with { MusicAnimationSelection = selection });
        }
        DialogResult = true;
    }

    private void LoadMusicAnimationOptions()
    {
        MusicAnimationSelectionComboBox.Items.Clear();
        MusicAnimationSelectionComboBox.Items.Add(new ComboBoxItem
        {
            Content = "每次开始播放或切歌时随机",
            Tag = MusicAnimationOptions.RandomSelection,
        });
        foreach (MusicAnimationOption option in MusicAnimationOptions.FixedOptions)
        {
            MusicAnimationSelectionComboBox.Items.Add(new ComboBoxItem
            {
                Content = $"固定循环：{option.DisplayName}",
                Tag = option.SelectionId,
            });
        }

        string selected = MusicAnimationOptions.NormalizeSelection(
            SelectedMediaPreferences.MusicAnimationSelection);
        MusicAnimationSelectionComboBox.SelectedItem =
            MusicAnimationSelectionComboBox.Items
                .OfType<ComboBoxItem>()
                .First(item => string.Equals(item.Tag as string, selected, StringComparison.Ordinal));
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
