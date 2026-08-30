using System.Windows;
using System.Windows.Controls;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class SettingsWindow : Window
{
    private readonly ISystemVolumeService? _systemVolumeService;
    private readonly IMessageNotificationSource? _messageNotificationSource;
    private bool _isReady;
    private bool _isUpdatingSlider;

    public SettingsWindow(
        VolumePreferences preferences,
        MessageNotificationPreferences notificationPreferences,
        ISystemVolumeService? systemVolumeService,
        IMessageNotificationSource? messageNotificationSource)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(notificationPreferences);
        SelectedPreferences = preferences;
        SelectedNotificationPreferences = notificationPreferences;
        _systemVolumeService = systemVolumeService;
        _messageNotificationSource = messageNotificationSource;
        InitializeComponent();

        MouseWheelControlCheckBox.IsChecked = preferences.EnableMouseWheelControl;
        ExternalFeedbackCheckBox.IsChecked = preferences.EnableExternalChangeFeedback;
        MessageReminderCheckBox.IsChecked = notificationPreferences.EnableMessageReminders;
        SelectWheelStep(preferences.MouseWheelStepPercent);
    }

    public VolumePreferences SelectedPreferences { get; private set; }

    public MessageNotificationPreferences SelectedNotificationPreferences { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isReady = true;
        UpdateNotificationAccessDisplay();
        if (_systemVolumeService is null)
        {
            VolumeSlider.IsEnabled = false;
            VolumeStatusText.Text = "暂时找不到系统音量设备；反馈设置仍可保存，下次启动会重试。";
            return;
        }

        _systemVolumeService.VolumeChanged += OnSystemVolumeChanged;
        UpdateVolumeDisplay(_systemVolumeService.Read(), "拖动滑块会立即调整系统音量。");
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_systemVolumeService is not null)
        {
            _systemVolumeService.VolumeChanged -= OnSystemVolumeChanged;
        }
    }

    private void OnSystemVolumeChanged(object? sender, SystemVolumeChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => UpdateVolumeDisplay(e.Snapshot, null));
    }

    private void OnVolumeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isReady || _isUpdatingSlider || _systemVolumeService is null)
        {
            return;
        }

        SystemVolumeAdjustmentResult result = _systemVolumeService.TrySetLevel((float)(e.NewValue / 100));
        if (result.Status is SystemVolumeAdjustmentStatus.Succeeded or SystemVolumeAdjustmentStatus.AtLimit)
        {
            UpdateVolumeDisplay(result.Snapshot, "系统音量已更新。", updateSlider: false);
            return;
        }

        VolumeStatusText.Text = result.Status switch
        {
            SystemVolumeAdjustmentStatus.ProtectedApplicationForeground =>
                "游戏安全模式：受保护游戏位于前台，没有调整音量。",
            SystemVolumeAdjustmentStatus.ForegroundCheckUnavailable =>
                "暂时无法确认前台程序，没有调整音量。",
            SystemVolumeAdjustmentStatus.EndpointUnavailable => "暂时找不到系统输出设备。",
            _ => "系统没有接受音量调整，请再试一次。",
        };
        UpdateVolumeDisplay(_systemVolumeService.Read(), null);
    }

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        VolumePreferences defaults = new();
        MouseWheelControlCheckBox.IsChecked = defaults.EnableMouseWheelControl;
        ExternalFeedbackCheckBox.IsChecked = defaults.EnableExternalChangeFeedback;
        SelectWheelStep(defaults.MouseWheelStepPercent);
        VolumeStatusText.Text = "已恢复反馈默认值；系统当前音量没有改变。";
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
        UpdateNotificationAccessDisplay(status);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SelectedPreferences = SelectedPreferences with
        {
            EnableMouseWheelControl = MouseWheelControlCheckBox.IsChecked == true,
            EnableExternalChangeFeedback = ExternalFeedbackCheckBox.IsChecked == true,
            MouseWheelStepPercent = ReadWheelStep(),
        };
        SelectedNotificationPreferences = SelectedNotificationPreferences with
        {
            EnableMessageReminders = MessageReminderCheckBox.IsChecked == true,
        };
        DialogResult = true;
    }

    private void UpdateNotificationAccessDisplay(MessageNotificationAccessStatus? knownStatus = null)
    {
        MessageNotificationAccessStatus status = knownStatus ??
            _messageNotificationSource?.GetAccessStatus() ??
            MessageNotificationAccessStatus.Unavailable;
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

    private void UpdateVolumeDisplay(
        SystemVolumeSnapshot snapshot,
        string? status,
        bool updateSlider = true)
    {
        if (!snapshot.IsAvailable)
        {
            VolumeSlider.IsEnabled = false;
            VolumeValueText.Text = "--%";
            if (status is not null)
            {
                VolumeStatusText.Text = status;
            }
            return;
        }

        VolumeSlider.IsEnabled = true;
        VolumeValueText.Text = snapshot.IsMuted
            ? $"静音 · {snapshot.Percentage}%"
            : $"{snapshot.Percentage}%";
        if (status is not null)
        {
            VolumeStatusText.Text = status;
        }

        if (!updateSlider)
        {
            return;
        }

        _isUpdatingSlider = true;
        VolumeSlider.Value = snapshot.Percentage;
        _isUpdatingSlider = false;
    }

    private void SelectWheelStep(int requestedStep)
    {
        int step = requestedStep is >= 1 and <= 20
            ? requestedStep
            : VolumePreferences.DefaultMouseWheelStepPercent;
        ComboBoxItem? match = WheelStepComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(
                item.Tag?.ToString(),
                step.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal));
        WheelStepComboBox.SelectedItem = match ?? WheelStepComboBox.Items[1];
    }

    private int ReadWheelStep() =>
        WheelStepComboBox.SelectedItem is ComboBoxItem item &&
        int.TryParse(item.Tag?.ToString(), out int step) &&
        step is >= 1 and <= 20
            ? step
            : VolumePreferences.DefaultMouseWheelStepPercent;
}
