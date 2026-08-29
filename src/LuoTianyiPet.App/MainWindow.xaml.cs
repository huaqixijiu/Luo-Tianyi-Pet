using System.IO;
using System.Windows;
using System.Windows.Input;
using LuoTianyiPet.Animation;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow : Window
{
    private const string EnjoyMusicAnimation = "resonance-enjoy-music";
    private const string CloseAnimation = "resonance-cracked-shake";
    private readonly ISettingsStore _settingsStore;
    private readonly IAppLogger _logger;
    private readonly AnimationCatalog? _animationCatalog;
    private readonly AnimationFramePlayer? _animationPlayer;
    private readonly bool _previewExit;
    private AppSettings _settings;
    private PetVisualState _visualState;
    private bool _isClosing;

    public MainWindow(
        AppSettings settings,
        ISettingsStore settingsStore,
        IAppLogger logger,
        AnimationCatalog? animationCatalog,
        PetVisualState initialVisualState,
        bool previewExit,
        bool showQaTaskbar)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _logger = logger;
        _animationCatalog = animationCatalog;
        _visualState = initialVisualState;
        _previewExit = previewExit;
        InitializeComponent();
        ShowInTaskbar = showQaTaskbar;
        _animationPlayer = animationCatalog is null
            ? null
            : new AnimationFramePlayer(PetImage, animationCatalog);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Topmost = _settings.Window.AlwaysOnTop;
        TopmostMenuItem.IsChecked = Topmost;
        FullBodyModeMenuItem.IsChecked = _visualState.SelectedDisplayMode == PetDisplayMode.FullBodyInteractive;

        Rect workArea = SystemParameters.WorkArea;
        double desiredLeft = _settings.Window.Left ?? workArea.Right - ActualWidth - 32;
        double desiredTop = _settings.Window.Top ?? workArea.Bottom - ActualHeight - 32;
        Left = Clamp(desiredLeft, workArea.Left, workArea.Right - ActualWidth);
        Top = Clamp(desiredTop, workArea.Top, workArea.Bottom - ActualHeight);

        PlayResolvedContinuousAnimation();
        if (_previewExit)
        {
            _ = BeginPreviewExitAsync();
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _isClosing)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleDisplayMode();
            e.Handled = true;
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException exception)
        {
            _logger.Error("window.drag_failed", exception);
        }
    }

    private void OnToggleDisplayMode(object sender, RoutedEventArgs e) => ToggleDisplayMode();

    private void ToggleDisplayMode()
    {
        PetDisplayMode nextMode = _visualState.SelectedDisplayMode == PetDisplayMode.Compact
            ? PetDisplayMode.FullBodyInteractive
            : PetDisplayMode.Compact;
        _visualState = _visualState with { SelectedDisplayMode = nextMode };
        FullBodyModeMenuItem.IsChecked = nextMode == PetDisplayMode.FullBodyInteractive;

        if (!_visualState.IsMusicPlaying)
        {
            PlayResolvedContinuousAnimation();
        }

        _logger.Info("display.mode_changed", nextMode.ToString());
    }

    private void OnPreviewMusicStart(object sender, RoutedEventArgs e)
    {
        _visualState = _visualState with { IsMusicPlaying = true };
        PlayAnimation(EnjoyMusicAnimation, PlayResolvedContinuousAnimation);
        _logger.Info("animation.preview_music_started", "M1 preview only.");
    }

    private void OnPreviewMusicStop(object sender, RoutedEventArgs e)
    {
        _visualState = _visualState with { IsMusicPlaying = false };
        PlayResolvedContinuousAnimation();
        _logger.Info("animation.preview_music_stopped", "Selected display mode restored.");
    }

    private void PlayResolvedContinuousAnimation() =>
        PlayAnimation(_visualState.ResolveContinuousAnimation());

    private void PlayAnimation(string animationId, Action? completed = null)
    {
        if (_animationPlayer is null || _animationCatalog is null)
        {
            ShowFallback("Animation catalog unavailable.");
            return;
        }

        try
        {
            AnimationAssetManifest manifest = _animationPlayer.Play(animationId, completed);
            PetImage.Width = manifest.DisplayWidth;
            PetImage.Height = manifest.DisplayHeight;
            PetImage.Visibility = Visibility.Visible;
            FallbackSurface.Visibility = Visibility.Collapsed;
            ResizeAroundBottomCenter(manifest.DisplayWidth + 16, manifest.DisplayHeight + 16);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or ArgumentException or KeyNotFoundException or NotSupportedException)
        {
            _logger.Error("animation.play_failed", exception);
            ShowFallback("Animation playback failed.");
        }
    }

    private void ShowFallback(string logMessage)
    {
        _animationPlayer?.Stop();
        PetImage.Visibility = Visibility.Collapsed;
        FallbackSurface.Visibility = Visibility.Visible;
        ResizeAroundBottomCenter(196, 196);
        _logger.Info("animation.fallback_shown", logMessage);
    }

    private void ResizeAroundBottomCenter(double width, double height)
    {
        Rect workArea = SystemParameters.WorkArea;
        double oldWidth = ActualWidth > 0 ? ActualWidth : Width;
        double oldHeight = ActualHeight > 0 ? ActualHeight : Height;
        double center = Left + oldWidth / 2;
        double bottom = Top + oldHeight;

        Width = width;
        Height = height;
        Left = Clamp(center - width / 2, workArea.Left, workArea.Right - width);
        Top = Clamp(bottom - height, workArea.Top, workArea.Bottom - height);
    }

    private void OnToggleTopmost(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostMenuItem.IsChecked;
        _logger.Info("window.topmost_changed", Topmost ? "Enabled." : "Disabled.");
    }

    private async void OnExitClick(object sender, RoutedEventArgs e)
    {
        await BeginUserRequestedExitAsync();
    }

    private async Task BeginPreviewExitAsync()
    {
        await Task.Delay(250);
        await BeginUserRequestedExitAsync();
    }

    private async Task BeginUserRequestedExitAsync()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        ExitMenuItem.IsEnabled = false;
        PlayAnimation(CloseAnimation);

        int[] offsets = [0, -8, 8, -7, 7, -5, 5, -3, 3, 0];
        foreach (int offset in offsets)
        {
            PetShakeTransform.X = offset;
            await Task.Delay(70);
        }

        PetShakeTransform.X = 0;
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _animationPlayer?.Dispose();
        _settings = _settings with
        {
            Window = _settings.Window with
            {
                AlwaysOnTop = Topmost,
                Left = Left,
                Top = Top,
            },
        };

        try
        {
            _settingsStore.SaveAsync(_settings).GetAwaiter().GetResult();
            _logger.Info("window.state_saved", "Window preferences saved.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error("window.state_save_failed", exception);
        }
    }

    private static double Clamp(double value, double minimum, double maximum) =>
        Math.Clamp(value, minimum, Math.Max(minimum, maximum));
}
