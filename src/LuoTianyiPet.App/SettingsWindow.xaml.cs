using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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
        Guard.NotNull(notificationPreferences, nameof(notificationPreferences));
        Guard.NotNull(windowPreferences, nameof(windowPreferences));
        Guard.NotNull(fileTreatPreferences, nameof(fileTreatPreferences));
        Guard.NotNull(appearancePreferences, nameof(appearancePreferences));
        Guard.NotNull(mediaPreferences, nameof(mediaPreferences));
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
        bool musicAnimationEnabled = SelectedMediaPreferences.MusicAnimationSelection !=
            MusicAnimationOptions.NoneSelection;
        MusicAnimationEnabledCheckBox.IsChecked = musicAnimationEnabled;
        SelectMusicAnimationCard(musicAnimationEnabled
            ? SelectedMediaPreferences.MusicAnimationSelection
            : MusicAnimationOptions.AutomaticSelection);
        UpdateMusicAnimationControlsEnabledState();
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
        string artwork = page switch
        {
            "Music" => "settings-sidebar-music.png",
            "Notification" => "settings-sidebar-notification.png",
            "About" => "settings-sidebar-about.png",
            _ => "settings-sidebar-general.png",
        };
        string artworkName = page switch
        {
            "Music" => "音乐页面装饰：真好",
            "Notification" => "通知页面装饰：天哪",
            "About" => "关于页面装饰：加入我们",
            _ => "常规页面装饰：我推",
        };
        UpdatePageCompanion(speech, artwork, artworkName);
    }

    private void UpdatePageCompanion(string speech, string artwork, string artworkName)
    {
        PageSpeechText.BeginAnimation(OpacityProperty, null);
        PageArtworkImage.BeginAnimation(OpacityProperty, null);
        PageSpeechText.Text = speech;
        PageArtworkImage.Source = new BitmapImage(
            new Uri($"pack://application:,,,/assets/ui/{artwork}", UriKind.Absolute));
        System.Windows.Automation.AutomationProperties.SetName(PageArtworkImage, artworkName);
        if (!SystemParameters.ClientAreaAnimation)
        {
            PageSpeechText.Opacity = 1;
            PageArtworkImage.Opacity = 0.95;
            return;
        }

        PageSpeechText.Opacity = 0;
        PageArtworkImage.Opacity = 0;
        DoubleAnimation fade = new(0, 1, TimeSpan.FromMilliseconds(160))
        {
            FillBehavior = FillBehavior.Stop,
        };
        fade.Completed += (_, _) => PageSpeechText.Opacity = 1;
        PageSpeechText.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        DoubleAnimation artworkFade = new(0, 0.95, TimeSpan.FromMilliseconds(160))
        {
            FillBehavior = FillBehavior.Stop,
        };
        artworkFade.Completed += (_, _) => PageArtworkImage.Opacity = 0.95;
        PageArtworkImage.BeginAnimation(
            OpacityProperty,
            artworkFade,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitializing)
        {
            SaveStatusText.Text = "有未保存的更改";
            SaveStatusText.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryDark");
        }
    }

    private void OnMusicAnimationEnabledChanged(object sender, RoutedEventArgs e)
    {
        UpdateMusicAnimationControlsEnabledState();
        OnSettingChanged(sender, e);
    }

    private void UpdateMusicAnimationControlsEnabledState()
    {
        bool enabled = MusicAnimationEnabledCheckBox.IsChecked == true;
        MusicAnimationSelectionPanel.IsEnabled = enabled;
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
        string selection = MusicAnimationEnabledCheckBox.IsChecked == true &&
            selectedMusicAnimation?.Tag is string selected
                ? selected
                : MusicAnimationOptions.NoneSelection;
        if (!string.IsNullOrWhiteSpace(selection))
        {
            SelectedMediaPreferences = MediaPreferences.Normalize(
                SelectedMediaPreferences with
                {
                    MusicAnimationSelection = selection,
                    // The former one-animation Easter-egg switch is retired. In
                    // intelligent mode both Luo Tianyi animations form the pool;
                    // fixed mode is artist-independent.
                    EnableLuoTianyiSingingEasterEgg = true,
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
                "Windows 通知已授权；桌宠也会尝试任务栏闪烁回退，且不读取正文。",
            MessageNotificationAccessStatus.Unspecified =>
                "尚未授权通知标题与图标。任务栏闪烁回退仍可识别 QQ / 微信来源。",
            MessageNotificationAccessStatus.Denied =>
                "Windows 已拒绝通知访问；仍会尝试任务栏闪烁回退，但只显示 QQ / 微信来源。",
            MessageNotificationAccessStatus.PackageIdentityRequired =>
                "当前是便携/普通 EXE，无法读取通知标题；任务栏闪烁回退仍可识别 QQ / 微信来源。",
            _ => "系统通知访问暂不可用；任务栏闪烁回退仍会尝试识别来源，其它功能不受影响。",
        };
        NotificationAccessButton.IsEnabled =
            status is MessageNotificationAccessStatus.Unspecified or
                MessageNotificationAccessStatus.Unavailable;
        NotificationAccessButton.Content = status == MessageNotificationAccessStatus.Allowed
            ? "已授权"
            : "授权访问";
    }

}
