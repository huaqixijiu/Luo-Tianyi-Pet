using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using LuoTianyiPet.Animation;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow : Window
{
    private const string CloseAnimation = "resonance-cracked-shake";
    private const string LandingAnimation = "codename-landing-bounce";
    private static readonly TimeSpan DoubleClickInterval = TimeSpan.FromMilliseconds(300);
    private readonly ISettingsStore _settingsStore;
    private readonly IAppLogger _logger;
    private readonly AnimationCatalog? _animationCatalog;
    private readonly AnimationFramePlayer? _animationPlayer;
    private readonly VisualSwapTransition _visualSwapTransition;
    private readonly LandingBounceMotion _landingBounceMotion;
    private readonly BodyReactionMotion _bodyReactionMotion;
    private readonly PointerGestureRecognizer _pointerGesture = new(6, DoubleClickInterval);
    private readonly PettingGestureRecognizer _pettingGesture = new(
        TimeSpan.FromMilliseconds(600),
        40,
        2,
        30);
    private readonly BodyHitMap _bodyHitMap = BodyHitMap.FullBodyDefault;
    private readonly BodyInteractionResolver _bodyInteractionResolver = new();
    private readonly MusicPlaybackAnimationSelector _musicAnimationSelector = new();
    private readonly DispatcherTimer _singleClickTimer;
    private readonly bool _previewExit;
    private readonly bool _previewMusicTransition;
    private readonly bool _previewBodyHitDebug;
    private readonly bool _previewDragCycle;
    private readonly string? _previewBodyReaction;
    private AppSettings _settings;
    private readonly PetStateMachine _stateMachine;
    private bool _isClosing;
    private bool _isWindowDragging;
    private bool _pettingGestureConsumedPress;
    private Point _dragPressScreenPoint;
    private double _dragStartLeft;
    private double _dragStartTop;
    private BodyRegionId? _lastDebugHitRegion;

    public MainWindow(
        AppSettings settings,
        ISettingsStore settingsStore,
        IAppLogger logger,
        AnimationCatalog? animationCatalog,
        PetVisualState initialVisualState,
        bool previewExit,
        bool previewMusicTransition,
        bool previewBodyHitDebug,
        bool previewDragCycle,
        string? previewBodyReaction,
        bool showQaTaskbar)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _logger = logger;
        _animationCatalog = animationCatalog;
        _stateMachine = new PetStateMachine(initialVisualState);
        _previewExit = previewExit;
        _previewMusicTransition = previewMusicTransition;
        _previewBodyHitDebug = previewBodyHitDebug;
        _previewDragCycle = previewDragCycle;
        _previewBodyReaction = previewBodyReaction;
        InitializeComponent();
        _visualSwapTransition = new VisualSwapTransition(
            PetVisual,
            PetScaleTransform,
            MusicTransitionFlash,
            MusicTransitionFlashScale);
        _landingBounceMotion = new LandingBounceMotion(PetShakeTransform);
        _bodyReactionMotion = new BodyReactionMotion(PetScaleTransform, PetShakeTransform);
        _singleClickTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = DoubleClickInterval,
        };
        _singleClickTimer.Tick += OnSingleClickTimerTick;
        ShowInTaskbar = showQaTaskbar;
        _animationPlayer = animationCatalog is null
            ? null
            : new AnimationFramePlayer(PetImage, animationCatalog);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Topmost = _settings.Window.AlwaysOnTop;
        TopmostMenuItem.IsChecked = Topmost;
        FullBodyModeMenuItem.IsChecked =
            _stateMachine.VisualState.SelectedDisplayMode == PetDisplayMode.FullBodyInteractive;
        BodyHitDebugMenuItem.IsChecked = _previewBodyHitDebug;

        Rect workArea = SystemParameters.WorkArea;
        double desiredLeft = _settings.Window.Left ?? workArea.Right - ActualWidth - 32;
        double desiredTop = _settings.Window.Top ?? workArea.Bottom - ActualHeight - 32;
        Left = Clamp(desiredLeft, workArea.Left, workArea.Right - ActualWidth);
        Top = Clamp(desiredTop, workArea.Top, workArea.Bottom - ActualHeight);

        PlayResolvedContinuousAnimation();
        UpdateBodyHitDebugOverlay();
        if (_previewExit)
        {
            _ = BeginPreviewExitAsync();
        }

        if (_previewMusicTransition)
        {
            _ = BeginPreviewMusicTransitionAsync();
        }

        if (_previewDragCycle)
        {
            _ = BeginPreviewDragCycleAsync();
        }

        if (_previewBodyReaction is not null)
        {
            _ = BeginPreviewBodyReactionAsync(_previewBodyReaction);
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _isClosing)
        {
            return;
        }

        Point position = e.GetPosition(this);
        _dragPressScreenPoint = GetPointerScreenPositionInDips(e);
        _dragStartLeft = Left;
        _dragStartTop = Top;
        Mouse.Capture(this);
        DateTimeOffset now = DateTimeOffset.Now;
        PointerGestureAction action = _pointerGesture.Press(ToPointerPoint(position), e.ClickCount, now);
        HandlePointerAction(action);
        if (action.Type == PointerGestureActionType.None && e.ClickCount == 1)
        {
            TryBeginPettingGesture(ToPointerPoint(position), now);
        }

        SyncSingleClickTimer();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isClosing || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        PointerPoint position = ToPointerPoint(e.GetPosition(this));
        if (_pettingGesture.IsTracking)
        {
            HandlePettingMove(position, DateTimeOffset.Now);
        }
        else if (!_pettingGestureConsumedPress)
        {
            HandlePointerAction(_pointerGesture.Move(position));
        }

        if (_isWindowDragging)
        {
            MoveWindowWithPointer(GetPointerScreenPositionInDips(e));
        }

        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _isClosing)
        {
            return;
        }

        if (_pettingGestureConsumedPress)
        {
            _pettingGestureConsumedPress = false;
            _pettingGesture.Cancel();
        }
        else
        {
            _pettingGesture.Cancel();
            HandlePointerAction(_pointerGesture.Release(
                ToPointerPoint(e.GetPosition(this)),
                DateTimeOffset.Now));
        }

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        SyncSingleClickTimer();
        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        _pettingGesture.Cancel();
        _pettingGestureConsumedPress = false;
        if (_isClosing || !_isWindowDragging)
        {
            return;
        }

        _pointerGesture.Cancel();
        EndWindowDrag();
    }

    private void TryBeginPettingGesture(PointerPoint windowPoint, DateTimeOffset now)
    {
        if (!_stateMachine.Resolve(now).BodyRegionInteractionsEnabled)
        {
            return;
        }

        PointerPoint? normalizedPoint = NormalizeToPetImage(windowPoint);
        if (normalizedPoint is null ||
            !IsOpaquePetPixel(normalizedPoint.Value) ||
            _bodyHitMap.HitTest(normalizedPoint.Value) != BodyRegionId.HeadAndHair)
        {
            return;
        }

        _pettingGesture.Begin(windowPoint, now);
        _logger.Info("interaction.petting_candidate_started", "HeadAndHair");
    }

    private void HandlePettingMove(PointerPoint windowPoint, DateTimeOffset now)
    {
        PointerPoint? normalizedPoint = NormalizeToPetImage(windowPoint);
        BodyHitRegion headRegion = _bodyHitMap.Regions.First(region => region.Id == BodyRegionId.HeadAndHair);
        if (normalizedPoint is null ||
            !headRegion.Bounds.Contains(normalizedPoint.Value) ||
            !IsOpaquePetPixel(normalizedPoint.Value))
        {
            _pettingGesture.Cancel();
            HandlePointerAction(_pointerGesture.Move(windowPoint));
            return;
        }

        PettingGestureAction action = _pettingGesture.Move(windowPoint, now);
        if (action == PettingGestureAction.YieldToWindowDrag)
        {
            HandlePointerAction(_pointerGesture.Move(windowPoint));
            return;
        }

        if (action != PettingGestureAction.Completed)
        {
            return;
        }

        _pettingGestureConsumedPress = true;
        _pointerGesture.Cancel();
        _singleClickTimer.Stop();
        BodyInteractionDecision decision = _bodyInteractionResolver.ResolvePetting();
        if (decision.AnimationId is string animationId)
        {
            _ = PlayBodyReactionAsync(animationId);
        }

        _logger.Info("interaction.petting_completed", "Cute reaction requested.");
    }

    private void OnToggleDisplayMode(object sender, RoutedEventArgs e) => ToggleDisplayMode();

    private void ToggleDisplayMode()
    {
        PetDisplayMode nextMode = _stateMachine.VisualState.SelectedDisplayMode == PetDisplayMode.Compact
            ? PetDisplayMode.FullBodyInteractive
            : PetDisplayMode.Compact;
        _stateMachine.SetDisplayMode(nextMode);
        FullBodyModeMenuItem.IsChecked = nextMode == PetDisplayMode.FullBodyInteractive;

        if (_stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous &&
            _stateMachine.VisualState.ContinuousState == PetContinuousState.Idle)
        {
            PlayResolvedContinuousAnimation();
        }

        _logger.Info("display.mode_changed", nextMode.ToString());
        UpdateBodyHitDebugOverlay();
    }

    private void OnSingleClickTimerTick(object? sender, EventArgs e)
    {
        PointerGestureAction action = _pointerGesture.FlushPendingSingleClick(DateTimeOffset.Now);
        if (action.Type == PointerGestureActionType.None)
        {
            SyncSingleClickTimer();
            return;
        }

        _singleClickTimer.Stop();
        HandlePointerAction(action);
    }

    private void HandlePointerAction(PointerGestureAction action)
    {
        switch (action.Type)
        {
            case PointerGestureActionType.None:
                break;
            case PointerGestureActionType.DispatchSingleClick:
                if (action.Position is PointerPoint clickPosition)
                {
                    HandleSingleClick(clickPosition);
                }
                break;
            case PointerGestureActionType.ToggleDisplayMode:
                _singleClickTimer.Stop();
                ToggleDisplayMode();
                break;
            case PointerGestureActionType.BeginDrag:
                BeginWindowDrag();
                break;
            case PointerGestureActionType.EndDrag:
                EndWindowDrag();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private void BeginWindowDrag()
    {
        _singleClickTimer.Stop();
        if (!_stateMachine.BeginDrag())
        {
            return;
        }

        _isWindowDragging = true;
        string? dragAnimation = _stateMachine.Resolve(DateTimeOffset.Now).AnimationId;
        if (_animationPlayer?.CurrentAnimationId != dragAnimation)
        {
            PlayResolvedContinuousAnimation();
        }

        _dragStartLeft = Left;
        _dragStartTop = Top;

        UpdateBodyHitDebugOverlay();
        _logger.Info("interaction.drag_started", _stateMachine.VisualState.SelectedDisplayMode.ToString());
    }

    private void MoveWindowWithPointer(Point currentScreenPoint)
    {
        double desiredLeft = _dragStartLeft + currentScreenPoint.X - _dragPressScreenPoint.X;
        double desiredTop = _dragStartTop + currentScreenPoint.Y - _dragPressScreenPoint.Y;
        Left = Clamp(
            desiredLeft,
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - ActualWidth);
        Top = Clamp(
            desiredTop,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - ActualHeight);
    }

    private void EndWindowDrag()
    {
        if (!_isWindowDragging)
        {
            return;
        }

        _isWindowDragging = false;
        if (_stateMachine.EndDrag())
        {
            PlayLandingFeedback();
        }

        _logger.Info("interaction.drag_ended", "Landing feedback requested.");
    }

    private void PlayLandingFeedback()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        ReactionStartOutcome outcome = _stateMachine.TryStartReaction(
            new ReactionRequest(
                LandingAnimation,
                ReactionPriority.UserInteraction,
                now.AddSeconds(3)),
            now);
        if (outcome.Token is not Guid token)
        {
            PlayResolvedContinuousAnimation();
            return;
        }

        PlayAnimation(
            LandingAnimation,
            () =>
            {
                _stateMachine.CompleteReaction(token, DateTimeOffset.Now);
                _landingBounceMotion.Cancel();
                PlayResolvedContinuousAnimation();
            });
        _landingBounceMotion.Play();
    }

    private void HandleSingleClick(PointerPoint windowPoint)
    {
        PetPlaybackPlan plan = _stateMachine.Resolve(DateTimeOffset.Now);
        if (!plan.BodyRegionInteractionsEnabled)
        {
            return;
        }

        PointerPoint? normalizedPoint = NormalizeToPetImage(windowPoint);
        if (normalizedPoint is null || !IsOpaquePetPixel(normalizedPoint.Value))
        {
            _lastDebugHitRegion = null;
            UpdateBodyHitDebugOverlay();
            return;
        }

        _lastDebugHitRegion = _bodyHitMap.HitTest(normalizedPoint.Value);
        UpdateBodyHitDebugOverlay();
        if (_lastDebugHitRegion is BodyRegionId region)
        {
            _logger.Info("interaction.body_hit", region.ToString());
            BodyInteractionDecision decision = _bodyInteractionResolver.Resolve(region, DateTimeOffset.Now);
            if (decision.Kind == BodyInteractionDecisionKind.PlayAnimation &&
                decision.AnimationId is string animationId)
            {
                _ = PlayBodyReactionAsync(animationId);
            }
            else
            {
                _logger.Info("interaction.body_hit_deferred", decision.Kind.ToString());
            }
        }
    }

    private async Task PlayBodyReactionAsync(string animationId)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        ReactionStartOutcome outcome = _stateMachine.TryStartReaction(
            new ReactionRequest(
                animationId,
                ReactionPriority.UserInteraction,
                now.AddSeconds(15)),
            now);
        if (outcome.Token is not Guid token)
        {
            _logger.Info("interaction.body_reaction_skipped", outcome.Result.ToString());
            return;
        }

        UpdateBodyHitDebugOverlay();
        bool transitioned = await _visualSwapTransition.PlayAsync(
            () => PlayAnimation(
                animationId,
                () => CompleteBodyReaction(token),
                preserveVisualTransition: true));
        if (transitioned && !_isClosing &&
            _animationPlayer?.CurrentAnimationId == animationId)
        {
            _bodyReactionMotion.PlayFor(animationId);
            _logger.Info("interaction.body_reaction_started", animationId);
        }
    }

    private void CompleteBodyReaction(Guid token)
    {
        if (!_stateMachine.CompleteReaction(token, DateTimeOffset.Now))
        {
            return;
        }

        _bodyReactionMotion.Cancel();
        _ = TransitionToResolvedContinuousAnimationAsync("animation.body_reaction_transition_completed");
    }

    private PointerPoint? NormalizeToPetImage(PointerPoint windowPoint)
    {
        if (PetImage.ActualWidth <= 0 || PetImage.ActualHeight <= 0)
        {
            return null;
        }

        Point imageOrigin = PetImage.TranslatePoint(new Point(0, 0), this);
        double x = (windowPoint.X - imageOrigin.X) / PetImage.ActualWidth;
        double y = (windowPoint.Y - imageOrigin.Y) / PetImage.ActualHeight;
        return x is >= 0 and <= 1 && y is >= 0 and <= 1
            ? new PointerPoint(x, y)
            : null;
    }

    private bool IsOpaquePetPixel(PointerPoint normalizedPoint)
    {
        if (PetImage.Source is not BitmapSource source)
        {
            return false;
        }

        if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Pbgra32)
        {
            return true;
        }

        int x = Math.Clamp((int)(normalizedPoint.X * source.PixelWidth), 0, source.PixelWidth - 1);
        int y = Math.Clamp((int)(normalizedPoint.Y * source.PixelHeight), 0, source.PixelHeight - 1);
        byte[] pixel = new byte[4];
        source.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel[3] >= 24;
    }

    private void OnToggleBodyHitDebug(object sender, RoutedEventArgs e) =>
        UpdateBodyHitDebugOverlay();

    private void OnPetImageSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateBodyHitDebugOverlay();

    private void UpdateBodyHitDebugOverlay()
    {
        bool isEnabled = BodyHitDebugMenuItem.IsChecked &&
            _stateMachine.Resolve(DateTimeOffset.Now).BodyRegionInteractionsEnabled &&
            PetImage.ActualWidth > 0 &&
            PetImage.ActualHeight > 0;
        BodyHitDebugOverlay.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        BodyHitDebugOverlay.Children.Clear();
        if (!isEnabled)
        {
            return;
        }

        foreach (BodyHitRegion region in _bodyHitMap.Regions.Reverse())
        {
            NormalizedRectangle bounds = region.Bounds;
            bool selected = region.Id == _lastDebugHitRegion;
            Rectangle rectangle = new()
            {
                Width = bounds.Width * PetImage.ActualWidth,
                Height = bounds.Height * PetImage.ActualHeight,
                Fill = new SolidColorBrush(Color.FromArgb(selected ? (byte)105 : (byte)42, 43, 220, 235)),
                Stroke = selected ? Brushes.Yellow : Brushes.White,
                StrokeThickness = selected ? 3 : 1,
            };
            Canvas.SetLeft(rectangle, bounds.X * PetImage.ActualWidth);
            Canvas.SetTop(rectangle, bounds.Y * PetImage.ActualHeight);
            BodyHitDebugOverlay.Children.Add(rectangle);

            TextBlock label = new()
            {
                Text = GetBodyRegionLabel(region.Id),
                Foreground = selected ? Brushes.Yellow : Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(150, 0, 55, 65)),
                FontSize = 8,
                Padding = new Thickness(2, 0, 2, 0),
            };
            Canvas.SetLeft(label, bounds.X * PetImage.ActualWidth + 2);
            Canvas.SetTop(label, bounds.Y * PetImage.ActualHeight + 2);
            BodyHitDebugOverlay.Children.Add(label);
        }
    }

    private void SyncSingleClickTimer()
    {
        _singleClickTimer.Stop();
        TimeSpan? remaining = _pointerGesture.TimeUntilPendingSingleClick(DateTimeOffset.Now);
        if (remaining is TimeSpan delay)
        {
            _singleClickTimer.Interval = delay < TimeSpan.FromMilliseconds(1)
                ? TimeSpan.FromMilliseconds(1)
                : delay;
            _singleClickTimer.Start();
        }
    }

    private Point GetPointerScreenPositionInDips(MouseEventArgs e)
    {
        Point physicalPoint = PointToScreen(e.GetPosition(this));
        PresentationSource? source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget is null
            ? physicalPoint
            : source.CompositionTarget.TransformFromDevice.Transform(physicalPoint);
    }

    private static PointerPoint ToPointerPoint(Point point) => new(point.X, point.Y);

    private static string GetBodyRegionLabel(BodyRegionId region) => region switch
    {
        BodyRegionId.LeftEye => "左眼",
        BodyRegionId.RightEye => "右眼",
        BodyRegionId.FaceAndMouth => "脸/嘴",
        BodyRegionId.LeftHand => "左手",
        BodyRegionId.RightHand => "右手",
        BodyRegionId.Chest => "胸部",
        BodyRegionId.LowerBodySensitiveArea => "下体",
        BodyRegionId.LeftFoot => "左脚",
        BodyRegionId.RightFoot => "右脚",
        BodyRegionId.HeadAndHair => "头发",
        BodyRegionId.OtherBody => "普通部位",
        _ => throw new ArgumentOutOfRangeException(nameof(region)),
    };

    private void OnPreviewMusicStart(object sender, RoutedEventArgs e)
    {
        StartMusicPreview();
    }

    private void StartMusicPreview()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        string selectedAnimation = _musicAnimationSelector.Select();
        _stateMachine.SetMusicAnimation(selectedAnimation);
        _stateMachine.SetContinuousState(PetContinuousState.MusicPlaying);
        PetPlaybackPlan plan = _stateMachine.Resolve(now);
        if (plan.Source == PlaybackPlanSource.Continuous)
        {
            _ = TransitionToResolvedContinuousAnimationAsync("animation.music_selection_transition_completed");
        }

        _logger.Info("animation.preview_music_started", $"Selected continuous animation: {selectedAnimation}.");
    }

    private void OnPreviewMusicStop(object sender, RoutedEventArgs e)
    {
        _stateMachine.SetContinuousState(PetContinuousState.Idle);
        if (_stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous)
        {
            _ = TransitionToResolvedContinuousAnimationAsync("animation.music_stopped_transition_completed");
        }

        _logger.Info("animation.preview_music_stopped", "Selected display mode restored.");
    }

    private void PlayResolvedContinuousAnimation(bool preserveVisualTransition = false)
    {
        PetPlaybackPlan plan = _stateMachine.Resolve(DateTimeOffset.Now);
        if (!plan.IsVisible || plan.AnimationId is null)
        {
            _animationPlayer?.Stop();
            PetImage.Visibility = Visibility.Collapsed;
            FallbackSurface.Visibility = Visibility.Collapsed;
            return;
        }

        PlayAnimation(plan.AnimationId, preserveVisualTransition: preserveVisualTransition);
    }

    private async Task TransitionToResolvedContinuousAnimationAsync(string completionEvent)
    {
        if (_animationPlayer is null || _animationCatalog is null || _isClosing)
        {
            PlayResolvedContinuousAnimation();
            return;
        }

        bool completed = await _visualSwapTransition.PlayAsync(
            () => PlayResolvedContinuousAnimation(preserveVisualTransition: true));
        if (completed && !_isClosing)
        {
            _logger.Info(completionEvent, "Pulse swap completed.");
        }
    }

    private void PlayAnimation(
        string animationId,
        Action? completed = null,
        bool preserveVisualTransition = false)
    {
        _landingBounceMotion.Cancel();
        _bodyReactionMotion.Cancel();
        if (!preserveVisualTransition)
        {
            CancelVisualTransition();
        }

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
            UpdateBodyHitDebugOverlay();
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
        BodyHitDebugOverlay.Visibility = Visibility.Collapsed;
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

    private async Task BeginPreviewMusicTransitionAsync()
    {
        await Task.Delay(500);
        if (!_isClosing)
        {
            StartMusicPreview();
        }
    }

    private async Task BeginPreviewDragCycleAsync()
    {
        await Task.Delay(500);
        if (_isClosing)
        {
            return;
        }

        BeginWindowDrag();
        await Task.Delay(900);
        if (!_isClosing)
        {
            EndWindowDrag();
        }
    }

    private async Task BeginPreviewBodyReactionAsync(string animationId)
    {
        await Task.Delay(500);
        if (!_isClosing)
        {
            await PlayBodyReactionAsync(animationId);
        }
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
        _singleClickTimer.Stop();
        _singleClickTimer.Tick -= OnSingleClickTimerTick;
        _pointerGesture.Cancel();
        _pettingGesture.Cancel();
        _landingBounceMotion.Cancel();
        _bodyReactionMotion.Cancel();
        CancelVisualTransition();
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

    private void CancelVisualTransition()
    {
        _visualSwapTransition.Cancel();
    }
}
