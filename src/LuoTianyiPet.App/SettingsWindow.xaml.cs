using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Animation;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class SettingsWindow : Window
{
    private readonly IMessageNotificationSource? _messageNotificationSource;
    private readonly IApplicationVolumeService? _applicationVolumeService;
    private bool _cloudMusicVolumeReady;
    private bool _updatingCloudMusicVolumeSlider;

    public SettingsWindow(
        MessageNotificationPreferences notificationPreferences,
        WindowPreferences windowPreferences,
        FileTreatPreferences fileTreatPreferences,
        AppearancePreferences appearancePreferences,
        MediaPreferences mediaPreferences,
        bool startupRegistrationEnabled,
        IMessageNotificationSource? messageNotificationSource,
        AnimationCatalog? animationCatalog,
        IApplicationVolumeService? applicationVolumeService)
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
        _applicationVolumeService = applicationVolumeService;
        InitializeComponent();
        LoadMusicAnimationOptions();

        MessageReminderCheckBox.IsChecked = notificationPreferences.EnableMessageReminders;
        StartWithWindowsCheckBox.IsChecked = startupRegistrationEnabled;
        AlwaysOnTopCheckBox.IsChecked = windowPreferences.AlwaysOnTop;
        DesktopFileTreatsCheckBox.IsChecked = fileTreatPreferences.EnableDesktopFileTreats;
        FullBodyLongHairRadio.IsChecked =
            SelectedAppearancePreferences.FullBodyStyle == AppearanceOptionIds.FullBodyLongHair;
        FullBodyCrystalDressRadio.IsChecked =
            SelectedAppearancePreferences.FullBodyStyle == AppearanceOptionIds.FullBodyCrystalDress;
        FullBodyClassicCatEarsRadio.IsChecked =
            SelectedAppearancePreferences.FullBodyStyle == AppearanceOptionIds.FullBodyClassicCatEars;
        BunEatingOriginalRadio.IsChecked =
            SelectedAppearancePreferences.BunEatingStyle == AppearanceOptionIds.BunEatingOriginal;
        BunEatingNewRadio.IsChecked =
            SelectedAppearancePreferences.BunEatingStyle == AppearanceOptionIds.BunEatingNew;
        LoadAppearancePreviews(animationCatalog);
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
        _cloudMusicVolumeReady = true;
        RefreshCloudMusicVolume();
    }

    private void OnRefreshCloudMusicVolumeClick(object sender, RoutedEventArgs e) =>
        RefreshCloudMusicVolume();

    private void OnCloudMusicVolumeSliderValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_cloudMusicVolumeReady ||
            _updatingCloudMusicVolumeSlider ||
            _applicationVolumeService is null)
        {
            return;
        }

        ApplicationVolumeAdjustmentResult result =
            _applicationVolumeService.TrySetLevel((float)(e.NewValue / 100));
        if (result.Status is ApplicationVolumeAdjustmentStatus.Succeeded or
            ApplicationVolumeAdjustmentStatus.AtLimit)
        {
            UpdateCloudMusicVolumeDisplay(
                result.Snapshot,
                result.Status == ApplicationVolumeAdjustmentStatus.Succeeded
                    ? "已同步到网易云音乐的应用音量。"
                    : "网易云音乐已经是这个音量。",
                updateSlider: false);
            return;
        }

        string status = result.Status switch
        {
            ApplicationVolumeAdjustmentStatus.TargetSessionMissing =>
                "没有找到网易云音频会话；请先播放一首歌后重新检测。",
            ApplicationVolumeAdjustmentStatus.ProtectedApplicationForeground =>
                "游戏安全模式：受保护游戏位于前台，没有调整音量。",
            ApplicationVolumeAdjustmentStatus.ForegroundCheckUnavailable =>
                "暂时无法确认前台程序，为安全起见没有调整音量。",
            ApplicationVolumeAdjustmentStatus.SessionUnavailable =>
                "Windows 音频服务暂时不可用，没有调整任何音量。",
            _ => "Windows 没有接受这次应用音量调整，请重新检测。",
        };
        RefreshCloudMusicVolume(status);
    }

    private void RefreshCloudMusicVolume(string? overrideStatus = null)
    {
        ApplicationVolumeSnapshot snapshot =
            _applicationVolumeService?.Read() ?? ApplicationVolumeSnapshot.Unavailable;
        string status = overrideStatus ?? (snapshot switch
        {
            { IsAvailable: true, SessionCount: > 1 } =>
                $"已连接网易云音乐的 {snapshot.SessionCount} 个音频会话。",
            { IsAvailable: true } => "已连接网易云音乐音频会话。",
            { ProbeSucceeded: true } =>
                "网易云尚未创建音频会话；请先播放一首歌后点“重新检测”。",
            _ => "Windows 音频服务暂时不可用；没有修改系统主音量。",
        });
        UpdateCloudMusicVolumeDisplay(snapshot, status);
    }

    private void UpdateCloudMusicVolumeDisplay(
        ApplicationVolumeSnapshot snapshot,
        string status,
        bool updateSlider = true)
    {
        CloudMusicVolumeSlider.IsEnabled = snapshot.IsAvailable;
        CloudMusicVolumeValueText.Text = snapshot.IsAvailable
            ? $"{snapshot.Percentage}%"
            : "--%";
        CloudMusicVolumeStatusText.Text = status;
        if (!updateSlider || !snapshot.IsAvailable)
        {
            return;
        }

        _updatingCloudMusicVolumeSlider = true;
        CloudMusicVolumeSlider.Value = snapshot.Percentage;
        _updatingCloudMusicVolumeSlider = false;
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
                FullBodyStyle = FullBodyCrystalDressRadio.IsChecked == true
                    ? AppearanceOptionIds.FullBodyCrystalDress
                    : FullBodyClassicCatEarsRadio.IsChecked == true
                        ? AppearanceOptionIds.FullBodyClassicCatEars
                        : AppearanceOptionIds.FullBodyLongHair,
                BunEatingStyle = BunEatingNewRadio.IsChecked == true
                    ? AppearanceOptionIds.BunEatingNew
                    : AppearanceOptionIds.BunEatingOriginal,
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

    private void LoadAppearancePreviews(AnimationCatalog? catalog)
    {
        if (catalog is null)
        {
            return;
        }

        FullBodyLongHairPreview.Source = TryCreateFramePreview(
            catalog,
            AppearanceOptionIds.LongHairAnimation,
            0);
        FullBodyCrystalDressPreview.Source = TryCreateFramePreview(
            catalog,
            AppearanceOptionIds.CrystalDressAnimation,
            0);
        FullBodyClassicCatEarsPreview.Source = TryCreateFramePreview(
            catalog,
            AppearanceOptionIds.ClassicCatEarsAnimation,
            0);
        BunEatingOriginalPreview.Source = TryCreateFramePreview(
            catalog,
            AppearanceOptionIds.OriginalBunEatAnimation,
            18);
        BunEatingNewPreview.Source = TryCreateFramePreview(
            catalog,
            AppearanceOptionIds.NewBunEatAnimation,
            18);
    }

    private static ImageSource? TryCreateFramePreview(
        AnimationCatalog catalog,
        string animationId,
        int requestedFrameIndex)
    {
        try
        {
            AnimationAssetManifest manifest = catalog.GetRequired(animationId);
            int frameIndex = Math.Clamp(
                requestedFrameIndex,
                0,
                manifest.FrameDurationsMilliseconds.Count - 1);
            BitmapImage atlas = new();
            atlas.BeginInit();
            atlas.CacheOption = BitmapCacheOption.OnLoad;
            atlas.UriSource = new Uri(catalog.GetAtlasPath(manifest), UriKind.Absolute);
            atlas.EndInit();
            atlas.Freeze();

            int column = frameIndex % manifest.Columns;
            int row = frameIndex / manifest.Columns;
            CroppedBitmap preview = new(
                atlas,
                new Int32Rect(
                    column * manifest.FrameWidth,
                    row * manifest.FrameHeight,
                    manifest.FrameWidth,
                    manifest.FrameHeight));
            preview.Freeze();
            return preview;
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or ArgumentException or
            KeyNotFoundException or NotSupportedException)
        {
            return null;
        }
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
