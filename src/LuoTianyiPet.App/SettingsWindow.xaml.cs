using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Animation;
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
        bool startupRegistrationEnabled,
        IMessageNotificationSource? messageNotificationSource,
        AnimationCatalog? animationCatalog)
    {
        ArgumentNullException.ThrowIfNull(notificationPreferences);
        ArgumentNullException.ThrowIfNull(windowPreferences);
        ArgumentNullException.ThrowIfNull(fileTreatPreferences);
        ArgumentNullException.ThrowIfNull(appearancePreferences);
        SelectedNotificationPreferences = notificationPreferences;
        SelectedWindowPreferences = windowPreferences;
        SelectedFileTreatPreferences = fileTreatPreferences;
        SelectedAppearancePreferences = AppearancePreferences.Normalize(appearancePreferences);
        StartWithWindowsSelected = startupRegistrationEnabled;
        _messageNotificationSource = messageNotificationSource;
        InitializeComponent();

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
                FullBodyStyle = FullBodyCrystalDressRadio.IsChecked == true
                    ? AppearanceOptionIds.FullBodyCrystalDress
                    : FullBodyClassicCatEarsRadio.IsChecked == true
                        ? AppearanceOptionIds.FullBodyClassicCatEars
                        : AppearanceOptionIds.FullBodyLongHair,
                BunEatingStyle = BunEatingNewRadio.IsChecked == true
                    ? AppearanceOptionIds.BunEatingNew
                    : AppearanceOptionIds.BunEatingOriginal,
            });
        DialogResult = true;
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
