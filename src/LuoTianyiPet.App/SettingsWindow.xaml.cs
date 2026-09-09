using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LuoTianyiPet.Core;
using WpfRadioButton = System.Windows.Controls.RadioButton;

namespace LuoTianyiPet.App;

public partial class SettingsWindow : Window
{
    private readonly IMessageNotificationSource? _messageNotificationSource;
    private bool _isInitializing = true;
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

        MessageReminderCheckBox.IsChecked = notificationPreferences.EnableMessageReminders;
        StartWithWindowsCheckBox.IsChecked = startupRegistrationEnabled;
        AlwaysOnTopCheckBox.IsChecked = windowPreferences.AlwaysOnTop;
        FullBodyStyleCyclingCheckBox.IsChecked =
            SelectedAppearancePreferences.EnableFullBodyStyleCycling;
        DesktopFileTreatsCheckBox.IsChecked = fileTreatPreferences.EnableDesktopFileTreats;
        LuoTianyiSingingEasterEggCheckBox.IsChecked =
            SelectedMediaPreferences.EnableLuoTianyiSingingEasterEgg;
        SelectMusicAnimationCard(SelectedMediaPreferences.MusicAnimationSelection);
        GeneralNavigationRadioButton.IsChecked = true;
        _isInitializing = false;
    }

    public MessageNotificationPreferences SelectedNotificationPreferences { get; private set; }

    public WindowPreferences SelectedWindowPreferences { get; private set; }

    public FileTreatPreferences SelectedFileTreatPreferences { get; private set; }

    public AppearancePreferences SelectedAppearancePreferences { get; private set; }

    public MediaPreferences SelectedMediaPreferences { get; private set; }

    public bool StartWithWindowsSelected { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Version? version = typeof(SettingsWindow).Assembly.GetName().Version;
        VersionTextBlock.Text = version is null
            ? "版本 0.1.0"
            : $"版本 {version.Major}.{version.Minor}.{version.Build}";
        UpdateNotificationAccessDisplay();
    }

    private void OnNavigationChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfRadioButton { Tag: string page })
        {
            return;
        }

        GeneralPage.Visibility = page == "General" ? Visibility.Visible : Visibility.Collapsed;
        MusicPage.Visibility = page == "Music" ? Visibility.Visible : Visibility.Collapsed;
        NotificationPage.Visibility = page == "Notification" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = page == "About" ? Visibility.Visible : Visibility.Collapsed;

        string speech = page switch
        {
            "Music" => "要一起听歌吗？",
            "Notification" => "有新消息的话，\n我会告诉你的。",
            "About" => "谢谢你让我\n留在桌面上～",
            _ => "今天也，\n一起加油吧～",
        };
        UpdatePageSpeech(speech);
    }

    private void UpdatePageSpeech(string speech)
    {
        PageSpeechText.BeginAnimation(OpacityProperty, null);
        PageSpeechText.Text = speech;
        if (!SystemParameters.ClientAreaAnimation)
        {
            PageSpeechText.Opacity = 1;
            return;
        }

        PageSpeechText.Opacity = 0;
        DoubleAnimation fade = new(0, 1, TimeSpan.FromMilliseconds(160))
        {
            FillBehavior = FillBehavior.Stop,
        };
        fade.Completed += (_, _) => PageSpeechText.Opacity = 1;
        PageSpeechText.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitializing)
        {
            SaveStatusText.Text = "有未保存的更改";
            SaveStatusText.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryDark");
        }
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
        OnSettingChanged(sender, e);
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
        WpfRadioButton? selectedMusicAnimation = MusicAnimationSelectionPanel.Children
            .OfType<WpfRadioButton>()
            .FirstOrDefault(option => option.IsChecked == true);
        if (selectedMusicAnimation?.Tag is string selection)
        {
            SelectedMediaPreferences = MediaPreferences.Normalize(
                SelectedMediaPreferences with
                {
                    MusicAnimationSelection = selection,
                    EnableLuoTianyiSingingEasterEgg =
                        LuoTianyiSingingEasterEggCheckBox.IsChecked == true,
                });
        }
        DialogResult = true;
    }

    private void SelectMusicAnimationCard(string? selection)
    {
        string selected = MusicAnimationOptions.NormalizeSelection(selection);
        WpfRadioButton card = MusicAnimationSelectionPanel.Children
            .OfType<WpfRadioButton>()
            .First(option => string.Equals(option.Tag as string, selected, StringComparison.Ordinal));
        card.IsChecked = true;
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
                "当前启动的是便携/普通 EXE，没有 Windows 应用包身份。请改用正式 MSIX 安装版；首次安装后再在这里授权。",
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
