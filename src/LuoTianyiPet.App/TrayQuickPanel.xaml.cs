using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using LuoTianyiPet.Core;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace LuoTianyiPet.App;

public partial class TrayQuickPanel : Window
{
    private const string DefaultCompanionText = "我一直在这里哦～";
    private readonly Action _openSettings;
    private readonly Func<bool> _isTopmostEnabled;
    private readonly Action<bool> _setTopmostEnabled;
    private readonly Func<bool> _isStartupEnabled;
    private readonly Action<bool> _setStartupEnabled;
    private readonly Func<int> _getDisplayScalePercent;
    private readonly Action<int> _previewDisplayScalePercent;
    private readonly Action<int> _commitDisplayScalePercent;
    private readonly Action _exit;
    private readonly DispatcherTimer _companionTextTimer;
    private readonly DispatcherTimer _exitConfirmationTimer;
    private bool _refreshingControls;

    public TrayQuickPanel(
        Action openSettings,
        Func<bool> isTopmostEnabled,
        Action<bool> setTopmostEnabled,
        Func<bool> isStartupEnabled,
        Action<bool> setStartupEnabled,
        Func<int> getDisplayScalePercent,
        Action<int> previewDisplayScalePercent,
        Action<int> commitDisplayScalePercent,
        Action exit)
    {
        _openSettings = openSettings;
        _isTopmostEnabled = isTopmostEnabled;
        _setTopmostEnabled = setTopmostEnabled;
        _isStartupEnabled = isStartupEnabled;
        _setStartupEnabled = setStartupEnabled;
        _getDisplayScalePercent = getDisplayScalePercent;
        _previewDisplayScalePercent = previewDisplayScalePercent;
        _commitDisplayScalePercent = commitDisplayScalePercent;
        _exit = exit;

        _refreshingControls = true;
        InitializeComponent();
        ScaleSlider.Minimum = AppearancePreferences.MinimumDisplayScalePercent;
        ScaleSlider.Maximum = AppearancePreferences.MaximumDisplayScalePercent;
        _refreshingControls = false;

        _companionTextTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.4),
        };
        _companionTextTimer.Tick += (_, _) =>
        {
            _companionTextTimer.Stop();
            SetCompanionText(DefaultCompanionText, restartTimer: false);
        };

        _exitConfirmationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        _exitConfirmationTimer.Tick += (_, _) => ResetExitConfirmation();
    }

    public void RefreshState()
    {
        _refreshingControls = true;
        TopmostToggle.IsChecked = _isTopmostEnabled();
        StartupToggle.IsChecked = _isStartupEnabled();
        int scale = Math.Clamp(
            _getDisplayScalePercent(),
            AppearancePreferences.MinimumDisplayScalePercent,
            AppearancePreferences.MaximumDisplayScalePercent);
        ScaleSlider.Value = scale;
        ScaleValueText.Text = $"{scale}%";
        _refreshingControls = false;
    }

    public void ShowNearTray()
    {
        RefreshState();
        ResetExitConfirmation();
        SetCompanionText(DefaultCompanionText, restartTimer: false);

        if (IsVisible)
        {
            Activate();
            return;
        }

        Left = -10000;
        Top = -10000;
        Opacity = 0;
        Show();
        UpdateLayout();
        PositionNearCursor();
        Activate();
        OpenSettingsButton.Focus();
        AnimateOpen();
    }

    public void HidePanel()
    {
        _companionTextTimer.Stop();
        _exitConfirmationTimer.Stop();
        Hide();
    }

    private void PositionNearCursor()
    {
        System.Drawing.Point cursor = Forms.Cursor.Position;
        Forms.Screen screen = Forms.Screen.FromPoint(cursor);
        System.Drawing.Rectangle work = screen.WorkingArea;
        double scale = GetMonitorScale(cursor);
        DesktopRectangle panelPixels = new(0, 0, Width * scale, Height * scale);
        TrayQuickPanelPosition position = TrayQuickPanelPlacement.Resolve(
            new PointerPoint(cursor.X, cursor.Y),
            panelPixels,
            new DesktopRectangle(work.Left, work.Top, work.Width, work.Height));
        nint handle = new WindowInteropHelper(this).Handle;
        _ = SetWindowPos(
            handle,
            0,
            (int)Math.Round(position.Left),
            (int)Math.Round(position.Top),
            0,
            0,
            0x0001 | 0x0004 | 0x0010);
    }

    private static double GetMonitorScale(System.Drawing.Point point)
    {
        NativePoint nativePoint = new(point.X, point.Y);
        nint monitor = MonitorFromPoint(nativePoint, 2);
        if (monitor != 0 && GetDpiForMonitor(monitor, 0, out uint dpiX, out _) == 0 && dpiX > 0)
        {
            return dpiX / 96d;
        }

        return 1;
    }

    private void AnimateOpen()
    {
        PanelOpenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        PanelOpenTranslate.Y = 8;
        QuadraticEase ease = new() { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = ease,
        });
        PanelOpenTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = ease,
        });
    }

    private void SetCompanionText(string text, bool restartTimer = true)
    {
        if (string.Equals(CompanionText.Text, text, StringComparison.Ordinal))
        {
            _companionTextTimer.Stop();
            if (restartTimer)
            {
                _companionTextTimer.Start();
            }

            return;
        }

        CompanionText.Text = text;
        CompanionText.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(150)));
        _companionTextTimer.Stop();
        if (restartTimer)
        {
            _companionTextTimer.Start();
        }
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        HidePanel();
        _openSettings();
    }

    private void OnTopmostToggleClick(object sender, RoutedEventArgs e)
    {
        if (_refreshingControls)
        {
            return;
        }

        bool enabled = TopmostToggle.IsChecked == true;
        _setTopmostEnabled(enabled);
        SetCompanionText(enabled ? "我会待在最上面陪你～" : "需要时再叫我到前面吧～");
    }

    private void OnStartupToggleClick(object sender, RoutedEventArgs e)
    {
        if (_refreshingControls)
        {
            return;
        }

        bool enabled = StartupToggle.IsChecked == true;
        _setStartupEnabled(enabled);
        SetCompanionText(enabled ? "下次开机也会见到我啦。" : "那我就等你下次手动叫我～");
    }

    private void OnScaleValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int value = (int)Math.Round(e.NewValue / 5d) * 5;
        value = Math.Clamp(
            value,
            AppearancePreferences.MinimumDisplayScalePercent,
            AppearancePreferences.MaximumDisplayScalePercent);
        ScaleValueText.Text = $"{value}%";
        if (_refreshingControls)
        {
            return;
        }

        _previewDisplayScalePercent(value);
        SetCompanionText("这样看起来合适吗？");
    }

    private void OnScalePreviewMouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e) => CommitScale();

    private void OnScaleKeyUp(object sender, WpfKeyEventArgs e) => CommitScale();

    private void OnDecreaseScaleClick(object sender, RoutedEventArgs e) => AdjustScale(-5);

    private void OnIncreaseScaleClick(object sender, RoutedEventArgs e) => AdjustScale(5);

    private void OnResetScaleClick(object sender, RoutedEventArgs e)
    {
        SetScaleAndCommit(100);
        SetCompanionText("恢复到原来的大小啦～");
    }

    private void AdjustScale(int delta) => SetScaleAndCommit((int)ScaleSlider.Value + delta);

    private void SetScaleAndCommit(int value)
    {
        value = Math.Clamp(
            value,
            AppearancePreferences.MinimumDisplayScalePercent,
            AppearancePreferences.MaximumDisplayScalePercent);
        ScaleSlider.Value = value;
        CommitScale();
    }

    private void CommitScale()
    {
        if (!_refreshingControls)
        {
            _commitDisplayScalePercent((int)Math.Round(ScaleSlider.Value / 5d) * 5);
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        ExitButton.Visibility = Visibility.Collapsed;
        ExitConfirmationPanel.Visibility = Visibility.Visible;
        _exitConfirmationTimer.Stop();
        _exitConfirmationTimer.Start();
    }

    private void OnCancelExitClick(object sender, RoutedEventArgs e) => ResetExitConfirmation();

    private void OnConfirmExitClick(object sender, RoutedEventArgs e)
    {
        HidePanel();
        _exit();
    }

    private void ResetExitConfirmation()
    {
        _exitConfirmationTimer.Stop();
        ExitConfirmationPanel.Visibility = Visibility.Collapsed;
        ExitButton.Visibility = Visibility.Visible;
    }

    private void OnDeactivated(object? sender, EventArgs e) => HidePanel();

    private void OnPreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HidePanel();
            e.Handled = true;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
