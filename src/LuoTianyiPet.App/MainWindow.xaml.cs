using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using LuoTianyiPet.Animation;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow : Window
{
    private const string CloseAnimation = "resonance-cracked-shake";
    private const string LandingAnimation = "codename-landing-bounce";
    private const string VolumeIncreaseAnimation = "resonance-voice";
    private const string VolumeDecreaseAnimation = "resonance-voice-reversed";
    private const string AwakeAnimation = "resonance-awake-pop";
    private const string GenshinLaunchAnimation = "resonance-no-playing";
    private const string GenshinCameoAnimation = "resonance-please";
    private const string MessageNotificationAnimation = "codename-curious-sway";
    private const double GenshinCameoSafeMargin = 24;
    private const double MediaControlsReservedHeight = 58;
    private const double TrackInfoReservedHeight = 52;
    private const double EdgeDockThreshold = 18;
    private const double EdgeDockVisibleStrip = 12;
    private static readonly TimeSpan DoubleClickInterval = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan BodyInteractionRecoveryDelay = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan TrackInfoAutomaticDisplayDuration = TimeSpan.FromSeconds(4);
    private readonly ISettingsStore _settingsStore;
    private readonly IAppLogger _logger;
    private readonly AnimationCatalog? _animationCatalog;
    private readonly AnimationFramePlayer? _animationPlayer;
    private readonly VisualSwapTransition _visualSwapTransition;
    private readonly LandingBounceMotion _landingBounceMotion;
    private readonly BodyReactionMotion _bodyReactionMotion;
    private readonly MediaControlsVisibilityMotion _mediaControlsMotion;
    private readonly MediaControlsVisibilityMotion _trackInfoMotion;
    private readonly PointerGestureRecognizer _pointerGesture = new(6, DoubleClickInterval);
    private readonly PettingGestureRecognizer _pettingGesture = new(
        TimeSpan.FromMilliseconds(350),
        24,
        1,
        24);
    private readonly BodyHitMap _bodyHitMap = BodyHitMap.FullBodyDefault;
    private readonly BodyInteractionResolver _bodyInteractionResolver = new();
    private readonly MusicPlaybackAnimationSelector _musicAnimationSelector = new();
    private readonly DispatcherTimer _singleClickTimer;
    private readonly DispatcherTimer _musicDetectionTimer;
    private readonly DispatcherTimer _feedbackBubbleTimer;
    private readonly DispatcherTimer _volumeFeedbackMergeTimer;
    private readonly DispatcherTimer _systemVolumePollTimer;
    private readonly DispatcherTimer _mediaControlsHideTimer;
    private readonly DispatcherTimer _trackInfoRefreshTimer;
    private readonly DispatcherTimer _trackInfoHideTimer;
    private readonly DispatcherTimer _idleSceneTimer;
    private readonly DispatcherTimer _genshinStatusTimer;
    private readonly DispatcherTimer _messageNotificationStatusTimer;
    private readonly IAudioSessionProbe? _audioSessionProbe;
    private readonly IMediaCommandSender _mediaCommandSender;
    private readonly ISystemVolumeService? _systemVolumeService;
    private readonly IStartupRegistrationService? _startupRegistrationService;
    private readonly IMediaTrackInfoSource? _mediaTrackInfoSource;
    private readonly IUserIdleTimeSource _userIdleTimeSource;
    private readonly ISystemResumeSource? _systemResumeSource;
    private readonly IProtectedGameProcessMonitor? _protectedGameMonitor;
    private readonly IForegroundApplicationProbe? _foregroundApplicationProbe;
    private readonly IMessageNotificationSource? _messageNotificationSource;
    private readonly IWindowWorkAreaProvider _windowWorkAreaProvider;
    private readonly BirthdayEasterEggScheduler _birthdayEasterEggScheduler = new();
    private readonly SystemResumeEventGate _systemResumeEventGate = new();
    private readonly GenshinBackgroundCameoScheduler _genshinCameoScheduler = new();
    private readonly RandomPetPositionSelector _randomPetPositionSelector = new();
    private readonly ProtectedGamePresenceTracker _genshinProcessMatcher;
    private readonly MessageProviderMatcher _messageProviderMatcher;
    private readonly MessageNotificationCoordinator _messageNotificationCoordinator;
    private readonly MusicAudioActivityDetector _musicActivityDetector;
    private readonly SystemVolumeChangeTracker _volumeChangeTracker = new();
    private readonly string _musicTargetProcessName;
    private readonly bool _previewExit;
    private readonly bool _previewMusicTransition;
    private readonly bool _previewBodyHitDebug;
    private readonly bool _previewDragCycle;
    private readonly bool _previewMediaControls;
    private readonly bool _previewTrackInfo;
    private readonly bool _previewLiveTrackInfo;
    private readonly bool _previewSettings;
    private readonly bool _previewTray;
    private readonly bool _previewSystemResume;
    private readonly bool _previewLongIdle;
    private readonly bool _previewGenshinLaunch;
    private readonly bool _previewGenshinCameo;
    private readonly MessageProvider? _previewMessageNotification;
    private readonly EdgeDockSide? _previewEdgeDock;
    private readonly string? _previewBodyReaction;
    private readonly bool _persistSettings;
    private AppSettings _settings;
    private readonly PetStateMachine _stateMachine;
    private bool _isClosing;
    private bool _isWindowDragging;
    private bool _pettingGestureConsumedPress;
    private bool _musicPreviewOverride;
    private bool _audioProbeFailureLogged;
    private bool _trackInfoProbeFailureLogged;
    private bool _trackInfoRefreshInFlight;
    private bool _trackInfoShowRequested;
    private bool _hasObservedTrackSnapshot;
    private bool _showNextTrackChange;
    private bool _externalVolumeFeedbackSubscribed;
    private bool _permanentTopmost;
    private CancellationTokenSource? _trackSwitchCancellation;
    private string _trackSwitchInitialIdentity = string.Empty;
    private string _musicAnimationTrackIdentity = string.Empty;
    private bool _trackSwitchSawAudioGap;
    private EdgeDockSide _edgeDockSide;
    private EdgeDockSide _dragEdgeCandidate;
    private bool _edgeDockRevealed;
    private int _edgeDockAnimationGeneration;
    private MediaTrackSnapshot _lastTrackSnapshot = MediaTrackSnapshot.Unavailable;
    private string _lastTrackIdentity = string.Empty;
    private Point _dragPressScreenPoint;
    private double _dragStartLeft;
    private double _dragStartTop;
    private BodyRegionId? _lastDebugHitRegion;
    private Guid? _activeVolumeReactionToken;
    private readonly HashSet<Guid> _transientTopmostRequests = [];
    private Guid? _genshinLaunchReactionToken;
    private Guid? _genshinLaunchTopmostToken;
    private Guid? _genshinCameoReactionToken;
    private Guid? _genshinCameoTopmostToken;
    private Point? _genshinCameoRestorePosition;
    private bool _pendingGenshinLaunch;
    private bool _systemSessionUnavailable;
    private bool _messageNotificationSubscribed;
    private Guid? _messageNotificationReactionToken;
    private Guid? _messageNotificationTopmostToken;
    private MessageProvider? _activeMessageProvider;
    private TrayIconController? _trayIcon;

    public MainWindow(
        AppSettings settings,
        ISettingsStore settingsStore,
        IAppLogger logger,
        AnimationCatalog? animationCatalog,
        IAudioSessionProbe? audioSessionProbe,
        IMediaCommandSender mediaCommandSender,
        ISystemVolumeService? systemVolumeService,
        IStartupRegistrationService? startupRegistrationService,
        IMediaTrackInfoSource? mediaTrackInfoSource,
        IUserIdleTimeSource userIdleTimeSource,
        ISystemResumeSource? systemResumeSource,
        IProtectedGameProcessMonitor? protectedGameMonitor,
        IForegroundApplicationProbe? foregroundApplicationProbe,
        IMessageNotificationSource? messageNotificationSource,
        IWindowWorkAreaProvider windowWorkAreaProvider,
        PetVisualState initialVisualState,
        bool previewExit,
        bool previewMusicTransition,
        bool previewBodyHitDebug,
        bool previewDragCycle,
        bool previewMediaControls,
        bool previewTrackInfo,
        bool previewLiveTrackInfo,
        bool previewSettings,
        bool previewTray,
        bool previewSystemResume,
        bool previewLongIdle,
        bool previewGenshinLaunch,
        bool previewGenshinCameo,
        MessageProvider? previewMessageNotification,
        EdgeDockSide? previewEdgeDock,
        string? previewBodyReaction,
        bool showQaTaskbar,
        bool persistSettings)
    {
        _settings = settings;
        _permanentTopmost = settings.Window.AlwaysOnTop;
        _settingsStore = settingsStore;
        _logger = logger;
        _animationCatalog = animationCatalog;
        _audioSessionProbe = audioSessionProbe;
        _mediaCommandSender = mediaCommandSender;
        _systemVolumeService = systemVolumeService;
        _startupRegistrationService = startupRegistrationService;
        _mediaTrackInfoSource = mediaTrackInfoSource;
        _userIdleTimeSource = userIdleTimeSource;
        _systemResumeSource = systemResumeSource;
        _protectedGameMonitor = protectedGameMonitor;
        _foregroundApplicationProbe = foregroundApplicationProbe;
        _messageNotificationSource = messageNotificationSource;
        _windowWorkAreaProvider = windowWorkAreaProvider;
        _genshinProcessMatcher = new ProtectedGamePresenceTracker(
            ParseGenshinProcessNames(settings.Genshin.ProcessNames));
        _messageProviderMatcher = new MessageProviderMatcher(settings.Notifications);
        _messageNotificationCoordinator = new MessageNotificationCoordinator(
            TimeSpan.FromMilliseconds(
                settings.Notifications.DuplicateWindowMilliseconds >= 0
                    ? settings.Notifications.DuplicateWindowMilliseconds
                    : MessageNotificationPreferences.DefaultDuplicateWindowMilliseconds));
        _musicTargetProcessName = string.IsNullOrWhiteSpace(settings.Media.TargetProcessName)
            ? "cloudmusic.exe"
            : settings.Media.TargetProcessName;
        float audiblePeakThreshold = float.IsFinite(settings.Media.AudiblePeakThreshold) &&
            settings.Media.AudiblePeakThreshold is > 0 and <= 1
                ? settings.Media.AudiblePeakThreshold
                : MediaPreferences.DefaultAudiblePeakThreshold;
        int silenceGraceMilliseconds = settings.Media.SilenceGraceMilliseconds >= 0
            ? settings.Media.SilenceGraceMilliseconds
            : MediaPreferences.DefaultSilenceGraceMilliseconds;
        _musicActivityDetector = new MusicAudioActivityDetector(
            audiblePeakThreshold,
            TimeSpan.FromMilliseconds(silenceGraceMilliseconds));
        _stateMachine = new PetStateMachine(initialVisualState);
        _previewExit = previewExit;
        _previewMusicTransition = previewMusicTransition;
        _previewBodyHitDebug = previewBodyHitDebug;
        _previewDragCycle = previewDragCycle;
        _previewMediaControls = previewMediaControls;
        _previewTrackInfo = previewTrackInfo;
        _previewLiveTrackInfo = previewLiveTrackInfo;
        _previewSettings = previewSettings;
        _previewTray = previewTray;
        _previewSystemResume = previewSystemResume;
        _previewLongIdle = previewLongIdle;
        _previewGenshinLaunch = previewGenshinLaunch;
        _previewGenshinCameo = previewGenshinCameo;
        _previewMessageNotification = previewMessageNotification;
        _previewEdgeDock = previewEdgeDock;
        _previewBodyReaction = previewBodyReaction;
        _persistSettings = persistSettings;
        InitializeComponent();
        _visualSwapTransition = new VisualSwapTransition(
            PetVisual,
            PetScaleTransform,
            MusicTransitionFlash,
            MusicTransitionFlashScale);
        _landingBounceMotion = new LandingBounceMotion(PetShakeTransform);
        _bodyReactionMotion = new BodyReactionMotion(PetScaleTransform, PetShakeTransform);
        _mediaControlsMotion = new MediaControlsVisibilityMotion(
            MediaControls,
            MediaControlsTranslate);
        _trackInfoMotion = new MediaControlsVisibilityMotion(
            TrackInfoBubble,
            TrackInfoTranslate,
            enableHitTesting: false);
        _singleClickTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = DoubleClickInterval,
        };
        _singleClickTimer.Tick += OnSingleClickTimerTick;
        _musicDetectionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(
                settings.Media.PollIntervalMilliseconds > 0
                    ? settings.Media.PollIntervalMilliseconds
                    : MediaPreferences.DefaultPollIntervalMilliseconds),
        };
        _musicDetectionTimer.Tick += OnMusicDetectionTimerTick;
        _feedbackBubbleTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2.4),
        };
        _feedbackBubbleTimer.Tick += OnFeedbackBubbleTimerTick;
        _volumeFeedbackMergeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(
                settings.Volume.MergeChangesWithinMilliseconds > 0
                    ? settings.Volume.MergeChangesWithinMilliseconds
                    : VolumePreferences.DefaultMergeChangesWithinMilliseconds),
        };
        _volumeFeedbackMergeTimer.Tick += OnVolumeFeedbackMergeTimerTick;
        _systemVolumePollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(
                settings.Volume.ExternalPollIntervalMilliseconds > 0
                    ? settings.Volume.ExternalPollIntervalMilliseconds
                    : VolumePreferences.DefaultExternalPollIntervalMilliseconds),
        };
        _systemVolumePollTimer.Tick += OnSystemVolumePollTimerTick;
        _mediaControlsHideTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(220),
        };
        _mediaControlsHideTimer.Tick += OnMediaControlsHideTimerTick;
        _trackInfoRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _trackInfoRefreshTimer.Tick += OnTrackInfoRefreshTimerTick;
        _trackInfoHideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TrackInfoAutomaticDisplayDuration,
        };
        _trackInfoHideTimer.Tick += OnTrackInfoHideTimerTick;
        _idleSceneTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _idleSceneTimer.Tick += OnIdleSceneTimerTick;
        _genshinStatusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(
                settings.Genshin.StatusPollIntervalMilliseconds > 0
                    ? settings.Genshin.StatusPollIntervalMilliseconds
                    : GenshinPreferences.DefaultStatusPollIntervalMilliseconds),
        };
        _genshinStatusTimer.Tick += OnGenshinStatusTimerTick;
        _messageNotificationStatusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _messageNotificationStatusTimer.Tick += OnMessageNotificationStatusTimerTick;
        ShowInTaskbar = showQaTaskbar;
        _animationPlayer = animationCatalog is null
            ? null
            : new AnimationFramePlayer(PetImage, animationCatalog);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyEffectiveTopmost();
        FullBodyModeMenuItem.IsChecked =
            _stateMachine.VisualState.SelectedDisplayMode == PetDisplayMode.FullBodyInteractive;
        BodyHitDebugMenuItem.IsChecked = _previewBodyHitDebug;
        PreviousTrackButton.ToolTip = $"上一首（{_settings.Media.PreviousTrackShortcut}）";
        TogglePlayPauseButton.ToolTip = $"播放 / 暂停（{_settings.Media.TogglePlayPauseShortcut}）";
        NextTrackButton.ToolTip = $"下一首（{_settings.Media.NextTrackShortcut}）";
        UpdatePlayPauseGlyph();
        _mediaControlsMotion.Hide(animate: false);
        _trackInfoMotion.Hide(animate: false);

        if (_systemVolumeService is not null)
        {
            ConfigureExternalVolumeFeedback(_settings.Volume.EnableExternalChangeFeedback);

            SystemVolumeSnapshot initialVolume = _systemVolumeService.Read();
            _volumeChangeTracker.Observe(initialVolume);
            _logger.Info(
                initialVolume.IsAvailable
                    ? "volume.endpoint_ready"
                    : "volume.endpoint_temporarily_unavailable",
                initialVolume.IsAvailable
                    ? "Default multimedia output endpoint is ready."
                    : "Default multimedia output endpoint could not be read.");
        }

        DesktopRectangle workArea = GetCurrentWorkArea();
        double desiredLeft = _settings.Window.Left ?? workArea.Right - ActualWidth - 32;
        double desiredTop = _settings.Window.Top ?? workArea.Bottom - ActualHeight - 32;
        Left = Clamp(desiredLeft, workArea.Left, workArea.Right - ActualWidth);
        Top = Clamp(desiredTop, workArea.Top, workArea.Bottom - ActualHeight);

        PlayResolvedContinuousAnimation();
        if (_persistSettings)
        {
            _ = PlayStartupGreetingAsync(DateTimeOffset.Now);
        }
        StartSystemResumeMonitoring();
        StartGenshinMonitoring();
        StartMessageNotificationMonitoring();
        if (!_previewLongIdle)
        {
            _idleSceneTimer.Start();
        }
        if (_audioSessionProbe is not null)
        {
            _musicDetectionTimer.Start();
            _logger.Info("media.detection_started", "Cloud music Core Audio detection enabled.");
        }

        if (_mediaTrackInfoSource is not null)
        {
            _trackInfoRefreshTimer.Start();
            _ = RefreshTrackInfoAsync(showWhenFound: false);
            _logger.Info("media.track_detection_started", "System media track detection enabled.");
        }

        UpdateBodyHitDebugOverlay();
        if (_persistSettings || _previewTray)
        {
            CreateTrayIcon();
        }
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
        if (_previewEdgeDock is EdgeDockSide edgeDockSide)
        {
            _ = BeginEdgeDockPreviewAsync(edgeDockSide);
        }

        if (_previewBodyReaction is not null)
        {
            _ = BeginPreviewBodyReactionAsync(_previewBodyReaction);
        }

        if (_previewMediaControls)
        {
            _ = BeginMediaControlsPreviewAsync();
        }

        if (_previewTrackInfo)
        {
            ShowTrackInfo(new MediaTrackSnapshot(true, true, "达拉崩吧", "洛天依"), holdAfterLeave: true);
        }
        else if (_previewLiveTrackInfo)
        {
            _ = BeginLiveTrackInfoPreviewAsync();
        }

        if (_previewSettings)
        {
            Dispatcher.BeginInvoke(ShowSettingsDialog, DispatcherPriority.ApplicationIdle);
        }
        if (_previewSystemResume)
        {
            _ = BeginSystemResumePreviewAsync();
        }
        if (_previewLongIdle)
        {
            _ = BeginLongIdlePreviewAsync();
        }
        if (_previewGenshinLaunch)
        {
            _ = BeginGenshinLaunchPreviewAsync();
        }
        if (_previewGenshinCameo)
        {
            _ = BeginGenshinCameoPreviewAsync();
        }
        if (_previewMessageNotification is MessageProvider provider)
        {
            _ = BeginMessageNotificationPreviewAsync(provider);
        }
    }

    private async Task PlayStartupGreetingAsync(DateTimeOffset now)
    {
        StartupTimeSceneDecision decision = StartupTimeSceneResolver.Resolve(
            TimeOnly.FromDateTime(now.LocalDateTime));
        _logger.Info("time.startup_greeting", decision.Scene.ToString());
        await PlayReactionAsync(decision.AnimationId, ReactionPriority.TimeGreeting);
    }

    private void StartSystemResumeMonitoring()
    {
        if (_systemResumeSource is null)
        {
            return;
        }

        try
        {
            _systemResumeSource.Resumed += OnSystemResumed;
            _systemResumeSource.Suspended += OnSystemSuspended;
            _systemResumeSource.Start();
            _logger.Info("time.resume_monitor_started", "Windows session and power resume monitoring started.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ExternalException)
        {
            _systemResumeSource.Resumed -= OnSystemResumed;
            _systemResumeSource.Suspended -= OnSystemSuspended;
            _logger.Error("time.resume_monitor_unavailable", exception);
        }
    }

    private void OnSystemResumed(object? sender, SystemResumeEventArgs e)
    {
        Dispatcher.BeginInvoke(() => HandleSystemResume(e));
    }

    private void HandleSystemResume(SystemResumeEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _systemSessionUnavailable = false;
        PetContinuousState continuousState = _stateMachine.VisualState.ContinuousState;
        if (continuousState is PetContinuousState.MediumIdle or PetContinuousState.Sleeping)
        {
            _stateMachine.SetContinuousState(PetContinuousState.Idle);
        }

        if (!_systemResumeEventGate.TryAccept(e.OccurredAt))
        {
            return;
        }

        if (_edgeDockSide != EdgeDockSide.None)
        {
            _logger.Info("time.resume_reaction_skipped", "Pet is intentionally hidden at a screen edge.");
            return;
        }

        _logger.Info("time.resume_reaction", e.Reason.ToString());
        _ = PlayReactionAsync(AwakeAnimation, ReactionPriority.System);
    }

    private void OnSystemSuspended(object? sender, SystemSuspendEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing)
            {
                return;
            }

            _systemSessionUnavailable = true;
            _genshinCameoScheduler.Reset();
            // Do not start or restore visual playback while Windows is locking or suspending.
            // The normal resume path will resolve the correct continuous state safely.
            CancelGenshinPresentations(restoreContinuousAnimation: false);
            _messageNotificationCoordinator.ClearPending();
            CancelMessageNotificationPresentation(restoreContinuousAnimation: false);
            _logger.Info("genshin.suspended", e.Reason.ToString());
        });
    }

    private async Task BeginSystemResumePreviewAsync()
    {
        await Task.Delay(700);
        if (!_isClosing)
        {
            HandleSystemResume(new SystemResumeEventArgs(
                SystemResumeReason.PowerResumed,
                DateTimeOffset.Now));
        }
    }

    private async Task BeginLongIdlePreviewAsync()
    {
        await Task.Delay(700);
        if (_isClosing)
        {
            return;
        }

        ApplyIdleScene(TimeSpan.FromMinutes(15));
        await Task.Delay(2400);
        if (!_isClosing)
        {
            ApplyIdleScene(TimeSpan.Zero);
        }
    }

    private void StartGenshinMonitoring()
    {
        if (_protectedGameMonitor is null)
        {
            return;
        }

        try
        {
            _protectedGameMonitor.PresenceChanged += OnProtectedGamePresenceChanged;
            _protectedGameMonitor.Start();
            _genshinStatusTimer.Start();
            _logger.Info(
                "genshin.monitor_started",
                _protectedGameMonitor.IsRunning
                    ? "Protected game was already running; launch reaction was not replayed."
                    : "Waiting for filtered low-frequency process enumeration.");
        }
        catch (InvalidOperationException exception)
        {
            _protectedGameMonitor.PresenceChanged -= OnProtectedGamePresenceChanged;
            _protectedGameMonitor.Dispose();
            _logger.Error("genshin.monitor_unavailable", exception);
        }
    }

    private void OnProtectedGamePresenceChanged(
        object? sender,
        ProtectedGamePresenceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => HandleProtectedGamePresenceChanged(e));
    }

    private void HandleProtectedGamePresenceChanged(ProtectedGamePresenceChangedEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        if (e.IsRunning)
        {
            _pendingGenshinLaunch = true;
            _logger.Info("genshin.process_started", "Protected game became running.");
            EvaluateGenshinIntegration();
            return;
        }

        _pendingGenshinLaunch = false;
        _genshinCameoScheduler.Reset();
        CancelGenshinPresentations(restoreContinuousAnimation: true);
        _logger.Info("genshin.process_stopped", "Protected game is no longer running.");
    }

    private void OnGenshinStatusTimerTick(object? sender, EventArgs e) =>
        EvaluateGenshinIntegration();

    private void EvaluateGenshinIntegration()
    {
        if (_isClosing || _protectedGameMonitor is null || _foregroundApplicationProbe is null)
        {
            return;
        }

        bool gameIsRunning = _protectedGameMonitor.IsRunning;
        if (!gameIsRunning)
        {
            _pendingGenshinLaunch = false;
            _genshinCameoScheduler.Reset();
            return;
        }

        ForegroundApplicationSnapshot foreground = _foregroundApplicationProbe.Query();
        bool gameIsForeground = foreground.Succeeded &&
            _genshinProcessMatcher.IsTargetProcess(foreground.ProcessName);
        bool unsafeForeground = !foreground.Succeeded || gameIsForeground || foreground.IsFullscreen;
        if (unsafeForeground || _systemSessionUnavailable)
        {
            _genshinCameoScheduler.Update(DateTimeOffset.Now, gameIsRunning, canShow: false);
            CancelGenshinPresentations(restoreContinuousAnimation: true);
            return;
        }

        bool windowAvailable = _edgeDockSide == EdgeDockSide.None &&
            !_isWindowDragging &&
            _stateMachine.VisualState.ContinuousState is not
                (PetContinuousState.Sleeping or PetContinuousState.HiddenForSafety);
        if (_pendingGenshinLaunch && windowAvailable)
        {
            _pendingGenshinLaunch = false;
            _ = BeginGenshinLaunchReactionAsync(retryOnFailure: true);
            return;
        }

        bool cameoCanShow = windowAvailable &&
            _genshinLaunchReactionToken is null &&
            _genshinCameoReactionToken is null &&
            _stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous;
        if (_genshinCameoScheduler.Update(
            DateTimeOffset.Now,
            gameIsRunning,
            cameoCanShow) == GenshinCameoScheduleDecision.Trigger)
        {
            _ = BeginGenshinCameoAsync();
        }
    }

    private async Task BeginGenshinLaunchReactionAsync(bool retryOnFailure)
    {
        if (_isClosing || _genshinLaunchReactionToken is not null)
        {
            return;
        }

        Guid topmostToken = AcquireTransientTopmost();
        Guid? reactionToken = await PlayReactionAsync(
            GenshinLaunchAnimation,
            ReactionPriority.Genshin);
        if (reactionToken is not Guid token)
        {
            ReleaseTransientTopmost(topmostToken);
            if (retryOnFailure && _protectedGameMonitor?.IsRunning == true)
            {
                _pendingGenshinLaunch = true;
            }
            return;
        }

        _genshinLaunchReactionToken = token;
        _genshinLaunchTopmostToken = topmostToken;
        _logger.Info("genshin.launch_reaction_started", "Three-loop reaction started without activation.");
    }

    private async Task BeginGenshinCameoAsync()
    {
        if (_isClosing || _genshinCameoReactionToken is not null || _edgeDockSide != EdgeDockSide.None)
        {
            return;
        }

        Point restorePosition = new(Left, Top);
        try
        {
            AnimationAssetManifest manifest = _animationCatalog?.GetRequired(GenshinCameoAnimation)
                ?? throw new InvalidOperationException("Genshin cameo animation is unavailable.");
            DesktopRectangle workArea = _windowWorkAreaProvider.GetForWindow(
                new WindowInteropHelper(this).Handle);
            double targetWidth = manifest.DisplayWidth + 16;
            double targetHeight = manifest.DisplayHeight + 16 +
                MediaControlsReservedHeight + TrackInfoReservedHeight;
            PointerPoint position = _randomPetPositionSelector.Select(
                workArea,
                Math.Max(ActualWidth, targetWidth),
                Math.Max(ActualHeight, targetHeight),
                GenshinCameoSafeMargin);
            Left = position.X;
            Top = position.Y;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentOutOfRangeException or KeyNotFoundException)
        {
            _logger.Error("genshin.cameo_position_unavailable", exception);
            return;
        }

        _genshinCameoRestorePosition = restorePosition;
        Guid topmostToken = AcquireTransientTopmost();
        Guid? reactionToken = await PlayReactionAsync(
            GenshinCameoAnimation,
            ReactionPriority.Genshin);
        if (reactionToken is not Guid token)
        {
            ReleaseTransientTopmost(topmostToken);
            RestoreWindowPosition(restorePosition);
            _genshinCameoRestorePosition = null;
            return;
        }

        _genshinCameoReactionToken = token;
        _genshinCameoTopmostToken = topmostToken;
        _logger.Info("genshin.cameo_started", "Background cameo started at a safe random work-area position.");
    }

    private async Task BeginGenshinLaunchPreviewAsync()
    {
        await Task.Delay(700);
        if (!_isClosing)
        {
            await BeginGenshinLaunchReactionAsync(retryOnFailure: false);
        }
    }

    private async Task BeginGenshinCameoPreviewAsync()
    {
        await Task.Delay(700);
        if (!_isClosing)
        {
            await BeginGenshinCameoAsync();
        }
    }

    private void StartMessageNotificationMonitoring()
    {
        if (_messageNotificationSource is null ||
            !_settings.Notifications.EnableMessageReminders ||
            !_settings.Notifications.WindowsNotificationAccessGranted)
        {
            return;
        }

        if (!_messageNotificationSubscribed)
        {
            _messageNotificationSource.NotificationReceived += OnMessageNotificationReceived;
            _messageNotificationSubscribed = true;
        }
        _messageNotificationSource.Start();
        _messageNotificationStatusTimer.Start();
        _logger.Info(
            "notification.monitor_status",
            _messageNotificationSource.GetAccessStatus().ToString());
    }

    private void OnMessageNotificationReceived(
        object? sender,
        MessageNotificationReceivedEventArgs e) =>
        Dispatcher.BeginInvoke(() => HandleMessageNotification(e));

    private void HandleMessageNotification(MessageNotificationReceivedEventArgs e)
    {
        if (_isClosing || !_settings.Notifications.EnableMessageReminders)
        {
            return;
        }

        ForegroundApplicationSnapshot foreground = _foregroundApplicationProbe?.Query() ??
            new ForegroundApplicationSnapshot(false, null, false);
        bool sourceIsForeground = foreground.Succeeded &&
            _messageProviderMatcher.IsForegroundProcess(e.Provider, foreground.ProcessName);
        bool canShow = IsMessageNotificationDisplaySafe(foreground);
        MessageNotificationDecision decision = _messageNotificationCoordinator.Observe(
            e.Provider,
            e.OccurredAt,
            sourceIsForeground,
            canShow);
        _logger.Info("notification.signal_processed", decision.ToString());
        if (decision == MessageNotificationDecision.Show)
        {
            _ = BeginMessageNotificationAsync(e.Provider, e.OccurredAt);
        }
    }

    private void OnMessageNotificationStatusTimerTick(object? sender, EventArgs e)
    {
        if (_isClosing || _foregroundApplicationProbe is null)
        {
            return;
        }

        ForegroundApplicationSnapshot foreground = _foregroundApplicationProbe.Query();
        if (!IsMessageNotificationDisplaySafe(foreground))
        {
            CancelMessageNotificationPresentation(restoreContinuousAnimation: true);
            return;
        }

        if (_activeMessageProvider is MessageProvider activeProvider &&
            _messageProviderMatcher.IsForegroundProcess(activeProvider, foreground.ProcessName))
        {
            CancelMessageNotificationPresentation(restoreContinuousAnimation: true);
            return;
        }

        if (_messageNotificationReactionToken is null &&
            _messageNotificationCoordinator.TryTakePending(
                provider => _messageProviderMatcher.IsForegroundProcess(
                    provider,
                    foreground.ProcessName),
                out MessageProvider pendingProvider))
        {
            _ = BeginMessageNotificationAsync(pendingProvider, DateTimeOffset.Now);
        }
    }

    private bool IsMessageNotificationDisplaySafe(ForegroundApplicationSnapshot foreground) =>
        foreground.Succeeded &&
        !foreground.IsFullscreen &&
        !_systemSessionUnavailable &&
        _edgeDockSide == EdgeDockSide.None &&
        !_isWindowDragging &&
        _stateMachine.VisualState.ContinuousState is not
            (PetContinuousState.Sleeping or PetContinuousState.HiddenForSafety) &&
        _stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous;

    private async Task BeginMessageNotificationAsync(
        MessageProvider provider,
        DateTimeOffset occurredAt)
    {
        if (_isClosing || _messageNotificationReactionToken is not null)
        {
            _messageNotificationCoordinator.QueuePending(provider, occurredAt);
            return;
        }

        Guid topmostToken = AcquireTransientTopmost();
        Guid? reactionToken = await PlayReactionAsync(
            MessageNotificationAnimation,
            ReactionPriority.Notification);
        if (reactionToken is not Guid token)
        {
            ReleaseTransientTopmost(topmostToken);
            _messageNotificationCoordinator.QueuePending(provider, occurredAt);
            return;
        }

        _messageNotificationReactionToken = token;
        _messageNotificationTopmostToken = topmostToken;
        _activeMessageProvider = provider;
        ShowFeedbackBubble($"{MessageProviderMatcher.GetDisplayName(provider)} 有新消息");
        _logger.Info("notification.reaction_started", "Source category was shown without message content.");
    }

    private async Task BeginMessageNotificationPreviewAsync(MessageProvider provider)
    {
        await Task.Delay(700);
        if (!_isClosing)
        {
            await BeginMessageNotificationAsync(provider, DateTimeOffset.Now);
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
        if (_edgeDockSide != EdgeDockSide.None)
        {
            return;
        }

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
        CancelGenshinPresentations(restoreContinuousAnimation: false);
        CancelMessageNotificationPresentation(restoreContinuousAnimation: false);
        if (_edgeDockSide != EdgeDockSide.None)
        {
            _edgeDockAnimationGeneration++;
            _edgeDockSide = EdgeDockSide.None;
            _edgeDockRevealed = false;
            SetEdgeMirror(false);
            EdgeDockHandle.Visibility = Visibility.Collapsed;
        }

        _dragEdgeCandidate = EdgeDockSide.None;

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
        UpdateDragEdgePreview();
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
            if (TryEnterEdgeDock())
            {
                _logger.Info("interaction.drag_ended", "Pet docked at a screen edge.");
                return;
            }

            _dragEdgeCandidate = EdgeDockSide.None;
            SetEdgeMirror(false);

            if (_stateMachine.VisualState.ContinuousState == PetContinuousState.MusicPlaying)
            {
                RestoreAfterMusicDrag();
                _logger.Info("interaction.drag_ended", "Music animation continued without landing feedback.");
            }
            else if (_stateMachine.VisualState.SelectedDisplayMode == PetDisplayMode.Compact)
            {
                PlayLandingFeedback();
                _logger.Info("interaction.drag_ended", "Compact landing feedback requested.");
            }
            else
            {
                RestoreAfterFullBodyDrag();
                _logger.Info("interaction.drag_ended", "Full-body mode restored without landing feedback.");
            }
        }
    }

    private void RestoreAfterMusicDrag()
    {
        PetPlaybackPlan plan = _stateMachine.Resolve(DateTimeOffset.Now);
        if (_animationPlayer?.CurrentAnimationId == plan.AnimationId)
        {
            UpdateBodyHitDebugOverlay();
            return;
        }

        _ = TransitionToResolvedContinuousAnimationAsync("animation.music_drag_restored");
    }

    private void RestoreAfterFullBodyDrag()
    {
        PetPlaybackPlan plan = _stateMachine.Resolve(DateTimeOffset.Now);
        if (_animationPlayer?.CurrentAnimationId == plan.AnimationId)
        {
            UpdateBodyHitDebugOverlay();
            return;
        }

        _ = TransitionToResolvedContinuousAnimationAsync("animation.full_body_drag_restored");
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
        if (_edgeDockSide != EdgeDockSide.None)
        {
            return;
        }

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
            else if (decision.Kind == BodyInteractionDecisionKind.PettingGestureRequired &&
                _bodyInteractionResolver.ResolvePetting().AnimationId is string headPatAnimation)
            {
                _ = PlayBodyReactionAsync(headPatAnimation);
            }
            else
            {
                _logger.Info("interaction.body_hit_deferred", decision.Kind.ToString());
            }
        }
    }

    private Task<Guid?> PlayBodyReactionAsync(string animationId) =>
        PlayReactionAsync(animationId, ReactionPriority.UserInteraction, suppressBodyAfter: true);

    private async Task<Guid?> PlayReactionAsync(
        string animationId,
        ReactionPriority priority,
        bool suppressBodyAfter = false)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        ReactionStartOutcome outcome = _stateMachine.TryStartReaction(
            new ReactionRequest(
                animationId,
                priority,
                now.AddSeconds(20)),
            now);
        if (outcome.Token is not Guid token)
        {
            _logger.Info("animation.reaction_skipped", outcome.Result.ToString());
            return null;
        }

        if (outcome.Result == ReactionStartResult.Replaced)
        {
            CleanupReplacedGenshinPresentation();
            CleanupReplacedMessageNotificationPresentation();
        }

        UpdateBodyHitDebugOverlay();
        bool transitioned = await _visualSwapTransition.PlayAsync(
            () => PlayAnimation(
                animationId,
                () => CompleteReaction(token, suppressBodyAfter),
                preserveVisualTransition: true));
        if (transitioned && !_isClosing &&
            _animationPlayer?.CurrentAnimationId == animationId)
        {
            _bodyReactionMotion.PlayFor(animationId);
            _logger.Info("animation.reaction_started", animationId);
            return token;
        }

        if (_stateMachine.ActiveReactionToken == token)
        {
            _stateMachine.CancelActiveReaction();
        }
        return null;
    }

    private void CompleteReaction(Guid token, bool suppressBodyAfter)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        if (!_stateMachine.CompleteReaction(token, now))
        {
            return;
        }

        if (suppressBodyAfter)
        {
            _stateMachine.SuppressBodyInteractions(now, BodyInteractionRecoveryDelay);
        }
        Point? restorePosition = FinishGenshinPresentation(token);
        FinishMessageNotificationPresentation(token);
        _bodyReactionMotion.Cancel();
        _ = TransitionToResolvedContinuousAnimationAsync(
            "animation.body_reaction_transition_completed",
            restorePosition is Point point
                ? () => RestoreWindowPosition(point)
                : null);
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
        BodyRegionId.Mouth => "嘴巴",
        BodyRegionId.Face => "脸部",
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
        _musicPreviewOverride = true;
        _musicActivityDetector.Reset();
        StartMusicPlayback("manual-preview", "洛天依");
    }

    private void StartMusicPlayback(string source, string? artistOverride = null)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        string artist = artistOverride ?? _lastTrackSnapshot.Artist;
        string selectedAnimation = _musicAnimationSelector.SelectForArtist(artist);
        _musicAnimationTrackIdentity = artistOverride is null
            ? _lastTrackIdentity
            : "preview-luo-tianyi";
        _stateMachine.SetMusicAnimation(selectedAnimation);
        _stateMachine.SetContinuousState(PetContinuousState.MusicPlaying);
        UpdatePlayPauseGlyph();
        PetPlaybackPlan plan = _stateMachine.Resolve(now);
        if (plan.Source == PlaybackPlanSource.Continuous &&
            _stateMachine.VisualState.ContinuousState != PetContinuousState.Dragging)
        {
            _ = TransitionToResolvedContinuousAnimationAsync("animation.music_selection_transition_completed");
        }

        _logger.Info(
            "media.playback_started",
            $"Source={source}; Animation={selectedAnimation}; ArtistClass={GetArtistClass(artist)}.");
    }

    private void OnPreviewMusicStop(object sender, RoutedEventArgs e)
    {
        _musicPreviewOverride = false;
        _musicActivityDetector.Reset();
        StopMusicPlayback("manual-preview");
    }

    private void StopMusicPlayback(string source)
    {
        _musicAnimationTrackIdentity = string.Empty;
        _stateMachine.SetContinuousState(PetContinuousState.Idle);
        UpdatePlayPauseGlyph();
        if (_stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous &&
            _stateMachine.VisualState.ContinuousState != PetContinuousState.Dragging)
        {
            _ = TransitionToResolvedContinuousAnimationAsync("animation.music_stopped_transition_completed");
        }

        _logger.Info("media.playback_stopped", $"Source={source}; Selected display mode restored.");
    }

    private void OnMusicDetectionTimerTick(object? sender, EventArgs e)
    {
        if (_audioSessionProbe is null || _musicPreviewOverride || _isClosing)
        {
            return;
        }

        AudioSessionSnapshot snapshot;
        try
        {
            snapshot = _audioSessionProbe.ReadForProcess(_musicTargetProcessName);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            System.Runtime.InteropServices.COMException or
            UnauthorizedAccessException)
        {
            if (!_audioProbeFailureLogged)
            {
                _audioProbeFailureLogged = true;
                _logger.Error("media.detection_probe_failed", exception);
            }

            return;
        }

        if (!snapshot.ProbeSucceeded)
        {
            if (!_audioProbeFailureLogged)
            {
                _audioProbeFailureLogged = true;
                _logger.Info("media.detection_temporarily_unavailable", "Core Audio probe will retry.");
            }

            return;
        }

        if (_audioProbeFailureLogged)
        {
            _audioProbeFailureLogged = false;
            _logger.Info("media.detection_recovered", "Core Audio probe resumed.");
        }

        MusicActivityTransition transition = _musicActivityDetector.Update(snapshot, DateTimeOffset.Now);
        if (transition == MusicActivityTransition.Started)
        {
            StartMusicPlayback("core-audio");
            if (_showNextTrackChange && _trackSwitchSawAudioGap)
            {
                ConfirmTrackSwitch("core-audio-resumed");
            }
        }
        else if (transition == MusicActivityTransition.Stopped)
        {
            if (_showNextTrackChange)
            {
                _trackSwitchSawAudioGap = true;
            }

            StopMusicPlayback("core-audio");
        }
    }

    private void UpdateDragEdgePreview()
    {
        EdgeDockSide candidate = ResolveCurrentEdgeDockSide();
        if (candidate == _dragEdgeCandidate)
        {
            return;
        }

        _dragEdgeCandidate = candidate;
        if (candidate == EdgeDockSide.None)
        {
            SetEdgeMirror(false);
            PlayResolvedContinuousAnimation();
            return;
        }

        SetEdgeMirror(candidate == EdgeDockSide.Left);
        PlayAnimation(GetEdgeDockAnimation(candidate));
        _logger.Info("interaction.drag_edge_preview", candidate.ToString());
    }

    private EdgeDockSide ResolveCurrentEdgeDockSide()
    {
        DesktopRectangle workArea = GetCurrentWorkArea();
        return EdgeDockResolver.Resolve(
            new DesktopRectangle(Left, Top, ActualWidth, ActualHeight),
            workArea,
            EdgeDockThreshold);
    }

    private void OnIdleSceneTimerTick(object? sender, EventArgs e)
    {
        if (_isClosing || _edgeDockSide != EdgeDockSide.None || _isWindowDragging)
        {
            return;
        }

        TimeSpan? idleDuration = _userIdleTimeSource.GetIdleDuration();
        if (idleDuration is null)
        {
            return;
        }

        ApplyIdleScene(idleDuration.Value);

        DateTimeOffset now = DateTimeOffset.Now;
        PetPlaybackPlan plan = _stateMachine.Resolve(now);
        bool birthdayEligible = plan.Source == PlaybackPlanSource.Continuous &&
            _stateMachine.VisualState.ContinuousState is PetContinuousState.Idle or PetContinuousState.MediumIdle;
        if (_birthdayEasterEggScheduler.ShouldTrigger(now, birthdayEligible))
        {
            _ = PlayReactionAsync(
                "twelfth-anniversary-happy-birthday",
                ReactionPriority.TimeGreeting);
            _logger.Info("animation.birthday_easter_egg", "Birthday idle easter egg requested.");
        }
    }

    private void ApplyIdleScene(TimeSpan idleDuration)
    {
        PetContinuousState previousState = _stateMachine.VisualState.ContinuousState;
        IdleSceneDecision decision = IdleSceneResolver.Resolve(idleDuration, previousState);
        if (!decision.ChangesStateFrom(previousState))
        {
            return;
        }

        _stateMachine.SetContinuousState(decision.TargetState);
        if (decision.PlayWakeReaction)
        {
            _logger.Info("animation.long_idle_wake", $"Restored={decision.TargetState}.");
            if (_systemResumeEventGate.TryAccept(DateTimeOffset.Now))
            {
                _ = PlayReactionAsync(AwakeAnimation, ReactionPriority.System);
            }
            else if (_stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous)
            {
                _ = TransitionToResolvedContinuousAnimationAsync(
                    "animation.long_idle_wake_merged.transition_completed");
            }
            return;
        }

        string eventName = decision.TargetState switch
        {
            PetContinuousState.MediumIdle => "animation.medium_idle_started",
            PetContinuousState.Sleeping => "animation.long_idle_sleep_started",
            _ => "animation.idle_restored",
        };
        _logger.Info(eventName, $"IdleMilliseconds={idleDuration.TotalMilliseconds:0}.");
        if (_stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous)
        {
            _ = TransitionToResolvedContinuousAnimationAsync(eventName + ".transition_completed");
        }
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
        if (!preserveVisualTransition)
        {
            _bodyReactionMotion.PlayFor(plan.AnimationId);
        }
    }

    private async Task TransitionToResolvedContinuousAnimationAsync(
        string completionEvent,
        Action? afterTransition = null)
    {
        if (_animationPlayer is null || _animationCatalog is null || _isClosing)
        {
            PlayResolvedContinuousAnimation();
            afterTransition?.Invoke();
            return;
        }

        bool completed = await _visualSwapTransition.PlayAsync(
            () => PlayResolvedContinuousAnimation(preserveVisualTransition: true));
        if (completed && !_isClosing)
        {
            StartResolvedContinuousMotion();
            afterTransition?.Invoke();
            _logger.Info(completionEvent, "Pulse swap completed.");
        }
        else if (!_isClosing)
        {
            afterTransition?.Invoke();
        }
    }

    private void StartResolvedContinuousMotion()
    {
        PetPlaybackPlan plan = _stateMachine.Resolve(DateTimeOffset.Now);
        if (plan.Source == PlaybackPlanSource.Continuous &&
            plan.AnimationId is string animationId &&
            _animationPlayer?.CurrentAnimationId == animationId)
        {
            _bodyReactionMotion.PlayFor(animationId);
        }
    }

    private void PlayAnimation(
        string animationId,
        Action? completed = null,
        bool preserveVisualTransition = false,
        bool reverse = false)
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
            AnimationAssetManifest manifest = _animationPlayer.Play(animationId, completed, reverse);
            PetImage.Width = manifest.DisplayWidth;
            PetImage.Height = manifest.DisplayHeight;
            PetImage.Visibility = Visibility.Visible;
            FallbackSurface.Visibility = Visibility.Collapsed;
            ResizeAroundBottomCenter(
                manifest.DisplayWidth + 16,
                manifest.DisplayHeight + 16 + MediaControlsReservedHeight + TrackInfoReservedHeight);
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
        ResizeAroundBottomCenter(
            196,
            196 + MediaControlsReservedHeight + TrackInfoReservedHeight);
        _logger.Info("animation.fallback_shown", logMessage);
    }

    private void ResizeAroundBottomCenter(double width, double height)
    {
        DesktopRectangle workArea = GetCurrentWorkArea();
        double oldWidth = ActualWidth > 0 ? ActualWidth : Width;
        double oldHeight = ActualHeight > 0 ? ActualHeight : Height;
        double center = Left + oldWidth / 2;
        double bottom = Top + oldHeight;

        Width = width;
        Height = height;
        Left = Clamp(center - width / 2, workArea.Left, workArea.Right - width);
        Top = Clamp(bottom - height, workArea.Top, workArea.Bottom - height);
    }

    private bool TryEnterEdgeDock()
    {
        EdgeDockSide side = _dragEdgeCandidate != EdgeDockSide.None
            ? _dragEdgeCandidate
            : ResolveCurrentEdgeDockSide();
        if (side == EdgeDockSide.None)
        {
            return false;
        }

        _dragEdgeCandidate = EdgeDockSide.None;
        _edgeDockSide = side;
        _edgeDockRevealed = false;
        int generation = ++_edgeDockAnimationGeneration;
        _stateMachine.CancelActiveReaction();
        _mediaControlsMotion.Hide(animate: false);
        _trackInfoMotion.Hide(animate: false);
        SetEdgeMirror(side == EdgeDockSide.Left);
        string animationId = GetEdgeDockAnimation(side);
        PlayAnimation(
            animationId,
            () =>
            {
                if (generation == _edgeDockAnimationGeneration && !_edgeDockRevealed)
                {
                    PositionEdgeDock(hidden: true);
                }
            },
            reverse: true);
        PositionEdgeDock(hidden: false);
        _logger.Info("window.edge_dock_started", side.ToString());
        return true;
    }

    private void RevealEdgeDock()
    {
        if (_edgeDockSide == EdgeDockSide.None || _edgeDockRevealed)
        {
            return;
        }

        _edgeDockRevealed = true;
        ++_edgeDockAnimationGeneration;
        PlayAnimation(GetEdgeDockAnimation(_edgeDockSide));
        PositionEdgeDock(hidden: false);
        _logger.Info("window.edge_dock_revealed", _edgeDockSide.ToString());
    }

    private void HideEdgeDock()
    {
        if (_edgeDockSide == EdgeDockSide.None || !_edgeDockRevealed)
        {
            return;
        }

        _edgeDockRevealed = false;
        int generation = ++_edgeDockAnimationGeneration;
        PlayAnimation(
            GetEdgeDockAnimation(_edgeDockSide),
            () =>
            {
                if (generation == _edgeDockAnimationGeneration && !_edgeDockRevealed)
                {
                    PositionEdgeDock(hidden: true);
                }
            },
            reverse: true);
        PositionEdgeDock(hidden: false);
        _logger.Info("window.edge_dock_hidden", _edgeDockSide.ToString());
    }

    private void PositionEdgeDock(bool hidden)
    {
        if (_edgeDockSide == EdgeDockSide.None)
        {
            return;
        }

        DesktopRectangle workArea = GetCurrentWorkArea();
        switch (_edgeDockSide)
        {
            case EdgeDockSide.Left:
                Left = hidden ? workArea.Left - ActualWidth + EdgeDockVisibleStrip : workArea.Left;
                Top = Clamp(Top, workArea.Top, workArea.Bottom - ActualHeight);
                break;
            case EdgeDockSide.Right:
                Left = hidden ? workArea.Right - EdgeDockVisibleStrip : workArea.Right - ActualWidth;
                Top = Clamp(Top, workArea.Top, workArea.Bottom - ActualHeight);
                break;
            case EdgeDockSide.Bottom:
                Left = Clamp(Left, workArea.Left, workArea.Right - ActualWidth);
                Top = hidden ? workArea.Bottom - EdgeDockVisibleStrip : workArea.Bottom - ActualHeight;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        ConfigureEdgeDockHandle(hidden);
    }

    private void ConfigureEdgeDockHandle(bool hidden)
    {
        if (!hidden || _edgeDockSide == EdgeDockSide.None)
        {
            EdgeDockHandle.Visibility = Visibility.Collapsed;
            return;
        }

        EdgeDockHandle.Visibility = Visibility.Visible;
        if (_edgeDockSide == EdgeDockSide.Bottom)
        {
            EdgeDockHandle.Width = 78;
            EdgeDockHandle.Height = EdgeDockVisibleStrip;
            EdgeDockHandle.HorizontalAlignment = WpfHorizontalAlignment.Center;
            EdgeDockHandle.VerticalAlignment = VerticalAlignment.Top;
            EdgeDockHandleDots.Orientation = Orientation.Horizontal;
        }
        else
        {
            EdgeDockHandle.Width = EdgeDockVisibleStrip;
            EdgeDockHandle.Height = 78;
            EdgeDockHandle.HorizontalAlignment = _edgeDockSide == EdgeDockSide.Left
                ? WpfHorizontalAlignment.Right
                : WpfHorizontalAlignment.Left;
            EdgeDockHandle.VerticalAlignment = VerticalAlignment.Center;
            EdgeDockHandleDots.Orientation = Orientation.Vertical;
        }
    }

    private void SetEdgeMirror(bool mirrored)
    {
        PetImage.RenderTransformOrigin = new Point(0.5, 0.5);
        PetImage.RenderTransform = mirrored ? new ScaleTransform(-1, 1) : Transform.Identity;
    }

    private static string GetEdgeDockAnimation(EdgeDockSide side) => side switch
    {
        EdgeDockSide.Left or EdgeDockSide.Right => "twelfth-anniversary-peek",
        EdgeDockSide.Bottom => "twelfth-anniversary-entrance",
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    private void CreateTrayIcon()
    {
        try
        {
            _trayIcon = new TrayIconController(
                () => Dispatcher.BeginInvoke(ShowSettingsDialog),
                () => _permanentTopmost,
                enabled => Dispatcher.BeginInvoke(() => SetPermanentTopmost(enabled, save: true)),
                () => _startupRegistrationService?.IsEnabled ?? false,
                enabled => Dispatcher.BeginInvoke(() => SetStartupEnabled(enabled, save: true)),
                () => Dispatcher.BeginInvoke(async () => await BeginUserRequestedExitAsync()));
            _logger.Info("tray.ready", "System tray controls are available.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception or
            ArgumentException)
        {
            _logger.Error("tray.initialization_failed", exception);
        }
    }

    private void SetPermanentTopmost(bool enabled, bool save)
    {
        _permanentTopmost = enabled;
        _settings = _settings with
        {
            Window = _settings.Window with { AlwaysOnTop = enabled },
        };
        ApplyEffectiveTopmost();
        _trayIcon?.RefreshChecks();
        _logger.Info("window.topmost_changed", enabled ? "Enabled." : "Disabled.");
        if (save && _persistSettings)
        {
            _ = SaveSettingsAsync("settings.topmost_saved", "Topmost preference saved.");
        }
    }

    private void SetStartupEnabled(bool enabled, bool save)
    {
        StartupRegistrationResult result = _startupRegistrationService?.TrySetEnabled(enabled) ??
            new StartupRegistrationResult(StartupRegistrationStatus.Unavailable, false);
        bool actual = result.IsEnabled;
        _settings = _settings with
        {
            Window = _settings.Window with { StartWithWindows = actual },
        };
        _trayIcon?.RefreshChecks();
        if (result.Status is StartupRegistrationStatus.Succeeded or StartupRegistrationStatus.Unchanged)
        {
            ShowFeedbackBubble(actual ? "已开启开机自启动" : "已关闭开机自启动");
            _logger.Info("startup.registration_changed", actual ? "Enabled." : "Disabled.");
            if (save && _persistSettings)
            {
                _ = SaveSettingsAsync("settings.startup_saved", "Startup preference saved.");
            }
        }
        else
        {
            ShowFeedbackBubble("开机自启动设置没有成功，请稍后再试");
            _logger.Info("startup.registration_rejected", result.Status.ToString());
        }
    }

    private Guid AcquireTransientTopmost()
    {
        Guid token = Guid.NewGuid();
        _transientTopmostRequests.Add(token);
        ApplyEffectiveTopmost();
        return token;
    }

    private void ReleaseTransientTopmost(Guid? token)
    {
        if (token is Guid value && _transientTopmostRequests.Remove(value))
        {
            ApplyEffectiveTopmost();
        }
    }

    private void ApplyEffectiveTopmost()
    {
        bool permanentTopmost = _permanentTopmost;
        bool effectiveTopmost = permanentTopmost || _transientTopmostRequests.Count > 0;
        Topmost = effectiveTopmost;
        nint handle = new WindowInteropHelper(this).Handle;
        if (handle != 0 &&
            !WindowsWindowZOrder.SetTopmostWithoutActivation(handle, effectiveTopmost) &&
            !permanentTopmost &&
            _transientTopmostRequests.Count > 0)
        {
            _transientTopmostRequests.Clear();
            Topmost = false;
            _logger.Info("window.transient_topmost_rejected", "No-activate z-order request failed closed.");
        }
    }

    private void CleanupReplacedGenshinPresentation()
    {
        Point? restorePosition = null;
        if (_genshinLaunchReactionToken is not null)
        {
            ReleaseTransientTopmost(_genshinLaunchTopmostToken);
            _genshinLaunchReactionToken = null;
            _genshinLaunchTopmostToken = null;
        }
        if (_genshinCameoReactionToken is not null)
        {
            ReleaseTransientTopmost(_genshinCameoTopmostToken);
            restorePosition = _genshinCameoRestorePosition;
            _genshinCameoReactionToken = null;
            _genshinCameoTopmostToken = null;
            _genshinCameoRestorePosition = null;
        }

        if (restorePosition is Point point)
        {
            RestoreWindowPosition(point);
        }
    }

    private void CleanupReplacedMessageNotificationPresentation()
    {
        if (_messageNotificationReactionToken is null)
        {
            return;
        }

        ReleaseTransientTopmost(_messageNotificationTopmostToken);
        _messageNotificationReactionToken = null;
        _messageNotificationTopmostToken = null;
        _activeMessageProvider = null;
    }

    private void FinishMessageNotificationPresentation(Guid reactionToken)
    {
        if (_messageNotificationReactionToken != reactionToken)
        {
            return;
        }

        ReleaseTransientTopmost(_messageNotificationTopmostToken);
        _messageNotificationReactionToken = null;
        _messageNotificationTopmostToken = null;
        _activeMessageProvider = null;
        _logger.Info("notification.reaction_completed", "Transient topmost was released.");
    }

    private void CancelMessageNotificationPresentation(bool restoreContinuousAnimation)
    {
        bool canceledActiveReaction =
            _messageNotificationReactionToken is Guid token &&
            _stateMachine.ActiveReactionToken == token;
        if (canceledActiveReaction)
        {
            _stateMachine.CancelActiveReaction();
        }

        CleanupReplacedMessageNotificationPresentation();
        if (canceledActiveReaction && restoreContinuousAnimation && !_isClosing)
        {
            _bodyReactionMotion.Cancel();
            _ = TransitionToResolvedContinuousAnimationAsync(
                "notification.presentation_cancelled");
        }
    }

    private Point? FinishGenshinPresentation(Guid reactionToken)
    {
        if (_genshinLaunchReactionToken == reactionToken)
        {
            ReleaseTransientTopmost(_genshinLaunchTopmostToken);
            _genshinLaunchReactionToken = null;
            _genshinLaunchTopmostToken = null;
            _logger.Info("genshin.launch_reaction_completed", "Transient topmost was released.");
        }

        if (_genshinCameoReactionToken != reactionToken)
        {
            return null;
        }

        ReleaseTransientTopmost(_genshinCameoTopmostToken);
        Point? restorePosition = _genshinCameoRestorePosition;
        _genshinCameoReactionToken = null;
        _genshinCameoTopmostToken = null;
        _genshinCameoRestorePosition = null;
        _logger.Info("genshin.cameo_completed", "Original position and topmost preference will be restored.");
        return restorePosition;
    }

    private void CancelGenshinPresentations(bool restoreContinuousAnimation)
    {
        bool canceledActiveReaction =
            _stateMachine.ActiveReactionToken == _genshinLaunchReactionToken ||
            _stateMachine.ActiveReactionToken == _genshinCameoReactionToken;
        if (canceledActiveReaction)
        {
            _stateMachine.CancelActiveReaction();
        }

        ReleaseTransientTopmost(_genshinLaunchTopmostToken);
        ReleaseTransientTopmost(_genshinCameoTopmostToken);
        Point? restorePosition = _genshinCameoRestorePosition;
        _genshinLaunchReactionToken = null;
        _genshinLaunchTopmostToken = null;
        _genshinCameoReactionToken = null;
        _genshinCameoTopmostToken = null;
        _genshinCameoRestorePosition = null;
        if (restorePosition is Point point)
        {
            RestoreWindowPosition(point);
        }

        if (canceledActiveReaction && restoreContinuousAnimation && !_isClosing)
        {
            _bodyReactionMotion.Cancel();
            _ = TransitionToResolvedContinuousAnimationAsync("genshin.presentation_cancelled");
        }
    }

    private void RestoreWindowPosition(Point position)
    {
        DesktopRectangle workArea = GetCurrentWorkArea();
        Left = Clamp(position.X, workArea.Left, workArea.Right - ActualWidth);
        Top = Clamp(position.Y, workArea.Top, workArea.Bottom - ActualHeight);
    }

    private DesktopRectangle GetCurrentWorkArea()
    {
        try
        {
            return _windowWorkAreaProvider.GetForWindow(new WindowInteropHelper(this).Handle);
        }
        catch (InvalidOperationException)
        {
            Rect fallback = SystemParameters.WorkArea;
            return new DesktopRectangle(fallback.Left, fallback.Top, fallback.Width, fallback.Height);
        }
    }

    private void ShowSettingsDialog()
    {
        if (_isClosing)
        {
            return;
        }

        SettingsWindow settingsWindow = new(
            _settings.Volume,
            _settings.Notifications,
            _settings.Window,
            _startupRegistrationService?.IsEnabled ?? false,
            _systemVolumeService,
            _messageNotificationSource)
        {
            Owner = this,
        };
        if (settingsWindow.ShowDialog() == true)
        {
            ApplyMessageNotificationPreferences(settingsWindow.SelectedNotificationPreferences);
            ApplyVolumePreferences(settingsWindow.SelectedPreferences);
            ApplyWindowPreferences(
                settingsWindow.SelectedWindowPreferences,
                settingsWindow.StartWithWindowsSelected);
        }
    }

    private void ApplyWindowPreferences(
        WindowPreferences preferences,
        bool startWithWindows)
    {
        SetPermanentTopmost(preferences.AlwaysOnTop, save: false);
        SetStartupEnabled(startWithWindows, save: false);
        _settings = _settings with
        {
            Window = _settings.Window with
            {
                AlwaysOnTop = _permanentTopmost,
                StartWithWindows = _startupRegistrationService?.IsEnabled ?? false,
                Left = Left,
                Top = Top,
            },
        };
        if (_persistSettings)
        {
            _ = SaveSettingsAsync("settings.window_saved", "Window preferences saved.");
        }
    }

    private void ApplyMessageNotificationPreferences(MessageNotificationPreferences preferences)
    {
        bool wasEnabled = _settings.Notifications.EnableMessageReminders;
        _settings = _settings with { Notifications = preferences };
        if (preferences.EnableMessageReminders)
        {
            StartMessageNotificationMonitoring();
        }
        else
        {
            _messageNotificationStatusTimer.Stop();
            _messageNotificationCoordinator.ClearPending();
            CancelMessageNotificationPresentation(restoreContinuousAnimation: true);
            _messageNotificationSource?.Stop();
        }

        if (wasEnabled != preferences.EnableMessageReminders)
        {
            _logger.Info(
                "notification.preferences_applied",
                preferences.EnableMessageReminders ? "Enabled." : "Disabled.");
        }
    }

    private void ApplyVolumePreferences(VolumePreferences preferences)
    {
        bool externalFeedbackWasEnabled = _settings.Volume.EnableExternalChangeFeedback;
        WindowPreferences currentWindow = _edgeDockSide == EdgeDockSide.None
            ? _settings.Window with
            {
                AlwaysOnTop = _permanentTopmost,
                Left = Left,
                Top = Top,
            }
            : _settings.Window with { AlwaysOnTop = _permanentTopmost };
        _settings = _settings with
        {
            Volume = preferences,
            Window = currentWindow,
        };
        _systemVolumeService?.UpdatePreferences(preferences);

        _volumeFeedbackMergeTimer.Interval = TimeSpan.FromMilliseconds(
            preferences.MergeChangesWithinMilliseconds > 0
                ? preferences.MergeChangesWithinMilliseconds
                : VolumePreferences.DefaultMergeChangesWithinMilliseconds);
        _systemVolumePollTimer.Interval = TimeSpan.FromMilliseconds(
            preferences.ExternalPollIntervalMilliseconds > 0
                ? preferences.ExternalPollIntervalMilliseconds
                : VolumePreferences.DefaultExternalPollIntervalMilliseconds);
        if (externalFeedbackWasEnabled != preferences.EnableExternalChangeFeedback)
        {
            ConfigureExternalVolumeFeedback(preferences.EnableExternalChangeFeedback);
        }

        if (_persistSettings)
        {
            _ = SaveSettingsAsync("settings.volume_saved", "Volume preferences saved.");
        }
        ShowFeedbackBubble("桌宠设置已保存");
        _logger.Info(
            "volume.preferences_applied",
            $"Wheel={preferences.EnableMouseWheelControl}; ExternalFeedback={preferences.EnableExternalChangeFeedback}; Step={preferences.MouseWheelStepPercent}.");
    }

    private void ConfigureExternalVolumeFeedback(bool enabled)
    {
        if (_systemVolumeService is null)
        {
            return;
        }

        if (enabled && !_externalVolumeFeedbackSubscribed)
        {
            _systemVolumeService.VolumeChanged += OnSystemVolumeChanged;
            _externalVolumeFeedbackSubscribed = true;
            _systemVolumePollTimer.Start();
            _volumeChangeTracker.Observe(_systemVolumeService.Read());
        }
        else if (!enabled && _externalVolumeFeedbackSubscribed)
        {
            _systemVolumeService.VolumeChanged -= OnSystemVolumeChanged;
            _externalVolumeFeedbackSubscribed = false;
            _systemVolumePollTimer.Stop();
        }
    }

    private async Task SaveSettingsAsync(string eventName, string message)
    {
        try
        {
            await _settingsStore.SaveAsync(_settings);
            _logger.Info(eventName, message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error("settings.save_failed", exception);
            ShowFeedbackBubble("设置暂时没有保存成功，请稍后再试");
        }
    }

    private void OnPreviousTrackClick(object sender, RoutedEventArgs e) =>
        TrySendMediaCommand(MediaCommand.PreviousTrack);

    private void OnTogglePlayPauseClick(object sender, RoutedEventArgs e) =>
        TrySendMediaCommand(MediaCommand.TogglePlayPause);

    private void OnNextTrackClick(object sender, RoutedEventArgs e) =>
        TrySendMediaCommand(MediaCommand.NextTrack);

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_systemVolumeService is null || _isClosing || e.Delta == 0)
        {
            return;
        }

        e.Handled = true;
        int steps = e.Delta > 0 ? 1 : -1;
        SystemVolumeAdjustmentResult result = _systemVolumeService.TryAdjustBySteps(steps);
        _logger.Info("volume.wheel_result", $"Status={result.Status}.");
        if (result.Status is SystemVolumeAdjustmentStatus.Succeeded or SystemVolumeAdjustmentStatus.AtLimit)
        {
            HandleVolumeSnapshot(
                result.Snapshot,
                showWhenUnchanged: true,
                requireFeedbackSafety: false);
            return;
        }

        string message = result.Status switch
        {
            SystemVolumeAdjustmentStatus.Disabled => "鼠标滚轮音量控制尚未启用",
            SystemVolumeAdjustmentStatus.ProtectedApplicationForeground =>
                "游戏安全模式：这次没有调整系统音量",
            SystemVolumeAdjustmentStatus.ForegroundCheckUnavailable =>
                "暂时无法确认前台程序，没有调整音量",
            SystemVolumeAdjustmentStatus.EndpointUnavailable => "暂时找不到系统输出设备",
            SystemVolumeAdjustmentStatus.SystemRejected => "系统没有接受音量调整，请再试一次",
            _ => "没有调整系统音量",
        };
        ShowFeedbackBubble(message);
        if (result.Status is SystemVolumeAdjustmentStatus.EndpointUnavailable or
            SystemVolumeAdjustmentStatus.SystemRejected)
        {
            _ = PlayReactionAsync("resonance-cry-shake", ReactionPriority.MediaOrVolume);
        }
    }

    private void OnSystemVolumeChanged(object? sender, SystemVolumeChangedEventArgs e)
    {
        if (_isClosing || _systemVolumeService is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing)
            {
                return;
            }

            _logger.Info("volume.endpoint_notification", "Default endpoint volume changed.");
            HandleVolumeSnapshot(
                e.Snapshot,
                showWhenUnchanged: false,
                requireFeedbackSafety: true);
        });
    }

    private void OnSystemVolumePollTimerTick(object? sender, EventArgs e)
    {
        if (_systemVolumeService is not null && !_isClosing)
        {
            HandleVolumeSnapshot(
                _systemVolumeService.Read(),
                showWhenUnchanged: false,
                requireFeedbackSafety: true);
        }
    }

    private void HandleVolumeSnapshot(
        SystemVolumeSnapshot snapshot,
        bool showWhenUnchanged,
        bool requireFeedbackSafety)
    {
        if (!snapshot.IsAvailable)
        {
            return;
        }

        SystemVolumeFeedbackDecision decision = _volumeChangeTracker.Observe(snapshot);
        if (!decision.ShouldShow && !showWhenUnchanged)
        {
            return;
        }

        if (requireFeedbackSafety &&
            _systemVolumeService?.CheckFeedbackSafety() != SystemVolumeSafetyStatus.Allowed)
        {
            _logger.Info("volume.feedback_suppressed", "Foreground safety check blocked feedback.");
            return;
        }

        string icon = snapshot.IsMuted ? "🔇" : "🔊";
        string state = decision.Kind switch
        {
            SystemVolumeChangeKind.Muted => "已静音 · ",
            SystemVolumeChangeKind.Unmuted => "已解除静音 · ",
            _ when snapshot.IsMuted => "已静音 · ",
            _ => string.Empty,
        };
        ShowFeedbackBubble($"{icon} {state}音量 {snapshot.Percentage}%");

        if (decision.ShouldAnimate)
        {
            StartOrMergeVolumeReaction(decision.Kind);
        }
    }

    private void StartOrMergeVolumeReaction(SystemVolumeChangeKind kind)
    {
        string animationId = kind == SystemVolumeChangeKind.Increased
            ? VolumeIncreaseAnimation
            : VolumeDecreaseAnimation;
        string mergeKey = kind == SystemVolumeChangeKind.Increased
            ? "volume:increase"
            : "volume:decrease";
        int cooldownMilliseconds = _settings.Volume.AnimationCooldownMilliseconds >= 0
            ? _settings.Volume.AnimationCooldownMilliseconds
            : VolumePreferences.DefaultAnimationCooldownMilliseconds;
        DateTimeOffset now = DateTimeOffset.Now;
        ReactionStartOutcome outcome = _stateMachine.TryStartReaction(
            new ReactionRequest(
                animationId,
                ReactionPriority.MediaOrVolume,
                now.AddSeconds(10),
                mergeKey,
                TimeSpan.FromMilliseconds(cooldownMilliseconds)),
            now);
        if (outcome.Token is not Guid token)
        {
            _logger.Info("volume.animation_skipped", outcome.Result.ToString());
            return;
        }

        _activeVolumeReactionToken = token;
        _volumeFeedbackMergeTimer.Stop();
        _volumeFeedbackMergeTimer.Start();
        if (outcome.Result == ReactionStartResult.Merged)
        {
            return;
        }

        _ = _visualSwapTransition.PlayAsync(
            () => PlayAnimation(animationId, preserveVisualTransition: true));
        _logger.Info("volume.animation_started", kind.ToString());
    }

    private void OnVolumeFeedbackMergeTimerTick(object? sender, EventArgs e)
    {
        _volumeFeedbackMergeTimer.Stop();
        if (_activeVolumeReactionToken is not Guid token)
        {
            return;
        }

        _activeVolumeReactionToken = null;
        if (_stateMachine.CompleteReaction(token, DateTimeOffset.Now))
        {
            _ = TransitionToResolvedContinuousAnimationAsync("volume.animation_completed");
        }
    }

    private void OnRootMouseEnter(object sender, MouseEventArgs e)
    {
        if (_edgeDockSide != EdgeDockSide.None)
        {
            RevealEdgeDock();
            return;
        }

        if (_settings.Media.EnableCloudMusicShortcutControl && !_isClosing)
        {
            _mediaControlsHideTimer.Stop();
            _mediaControlsMotion.Show();
        }

        if (!_isClosing)
        {
            _trackInfoHideTimer.Stop();
            if (_lastTrackSnapshot.HasTrack)
            {
                ShowTrackInfo(_lastTrackSnapshot, holdAfterLeave: false);
            }

            _ = RefreshTrackInfoAsync(showWhenFound: true);
        }
    }

    private void OnEdgeDockHandleMouseEnter(object sender, MouseEventArgs e)
    {
        if (_edgeDockSide != EdgeDockSide.None)
        {
            RevealEdgeDock();
            e.Handled = true;
        }
    }

    private void OnRootMouseLeave(object sender, MouseEventArgs e)
    {
        if (_edgeDockSide != EdgeDockSide.None)
        {
            if (!_isWindowDragging)
            {
                HideEdgeDock();
            }

            return;
        }

        if (!_previewMediaControls)
        {
            _mediaControlsHideTimer.Stop();
            _mediaControlsHideTimer.Start();
        }

        if (!_previewTrackInfo && !_trackInfoHideTimer.IsEnabled)
        {
            _trackInfoMotion.Hide();
        }
    }

    private void OnMediaControlsHideTimerTick(object? sender, EventArgs e)
    {
        _mediaControlsHideTimer.Stop();
        if (!_previewMediaControls && !IsMouseOver)
        {
            _mediaControlsMotion.Hide();
        }
    }

    private void UpdatePlayPauseGlyph()
    {
        bool isPlaying = _stateMachine.VisualState.ContinuousState == PetContinuousState.MusicPlaying;
        PlayGlyph.Visibility = isPlaying ? Visibility.Collapsed : Visibility.Visible;
        PauseGlyph.Visibility = isPlaying ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TrySendMediaCommand(MediaCommand command)
    {
        MediaCommandSendResult result = _mediaCommandSender.TrySend(command, DateTimeOffset.Now);
        _logger.Info("media.command_result", $"Command={command}; Status={result.Status}.");

        string message = result.Status switch
        {
            MediaCommandSendStatus.Sent => "快捷键已发送，等待播放器响应",
            MediaCommandSendStatus.Disabled => "网易云快捷键控制尚未启用",
            MediaCommandSendStatus.InvalidShortcut => "快捷键设置无效，请检查配置",
            MediaCommandSendStatus.ProtectedApplicationForeground => "游戏安全模式：这次没有发送快捷键",
            MediaCommandSendStatus.ForegroundCheckUnavailable => "暂时无法确认前台程序，请稍后再试",
            MediaCommandSendStatus.KeyboardBusy => "键盘正在使用，请松开按键后再试",
            MediaCommandSendStatus.RateLimited => "操作太快啦，请稍等一下",
            MediaCommandSendStatus.SystemRejected => "系统没有接受快捷键，请再试一次",
            _ => "没有发送快捷键",
        };
        ShowFeedbackBubble(message);

        if (result.WasSent && command is MediaCommand.PreviousTrack or MediaCommand.NextTrack)
        {
            _trackSwitchCancellation?.Cancel();
            _trackSwitchCancellation?.Dispose();
            _trackSwitchCancellation = new CancellationTokenSource();
            _trackSwitchInitialIdentity = _lastTrackIdentity;
            _trackSwitchSawAudioGap = false;
            _showNextTrackChange = true;
            ShowTrackSwitchPending();
            _ = MonitorTrackSwitchAsync(_trackSwitchCancellation.Token);
        }

        if (!result.WasSent && result.Status is not MediaCommandSendStatus.RateLimited)
        {
            _ = PlayBodyReactionAsync("resonance-cry-shake");
        }
    }

    private void ShowFeedbackBubble(string message)
    {
        FeedbackBubbleText.Text = message;
        FeedbackBubble.Visibility = Visibility.Visible;
        _feedbackBubbleTimer.Stop();
        _feedbackBubbleTimer.Start();
    }

    private void OnFeedbackBubbleTimerTick(object? sender, EventArgs e)
    {
        _feedbackBubbleTimer.Stop();
        FeedbackBubble.Visibility = Visibility.Collapsed;
    }

    private async void OnTrackInfoRefreshTimerTick(object? sender, EventArgs e)
    {
        await RefreshTrackInfoAsync(showWhenFound: false);
    }

    private async Task RefreshTrackInfoAsync(bool showWhenFound)
    {
        _trackInfoShowRequested |= showWhenFound;
        if (_mediaTrackInfoSource is null || _trackInfoRefreshInFlight || _isClosing)
        {
            if (showWhenFound && _mediaTrackInfoSource is null)
            {
                ShowTrackInfoUnavailable();
                _trackInfoShowRequested = false;
            }

            return;
        }

        _trackInfoRefreshInFlight = true;
        try
        {
            MediaTrackSnapshot snapshot = MediaTrackText.Normalize(
                await _mediaTrackInfoSource.ReadAsync(_musicTargetProcessName));
            bool shouldShowWhenFound = showWhenFound || _trackInfoShowRequested;
            if (!snapshot.ProbeSucceeded)
            {
                if (!_trackInfoProbeFailureLogged)
                {
                    _trackInfoProbeFailureLogged = true;
                    _logger.Info(
                        "media.track_detection_temporarily_unavailable",
                        "System media track probe will retry.");
                }

                if (shouldShowWhenFound && !_lastTrackSnapshot.HasTrack)
                {
                    ShowTrackInfoUnavailable();
                }

                return;
            }

            if (_trackInfoProbeFailureLogged)
            {
                _trackInfoProbeFailureLogged = false;
                _logger.Info("media.track_detection_recovered", "System media track probe resumed.");
            }

            string identity = snapshot.HasTrack
                ? $"{snapshot.Title}\u001f{snapshot.Artist}"
                : string.Empty;
            bool trackChanged = _hasObservedTrackSnapshot &&
                snapshot.HasTrack &&
                !identity.Equals(_lastTrackIdentity, StringComparison.Ordinal);
            _hasObservedTrackSnapshot = true;
            _lastTrackSnapshot = snapshot;
            _lastTrackIdentity = identity;

            if (snapshot.HasTrack &&
                _musicActivityDetector.IsPlaying &&
                !identity.Equals(_musicAnimationTrackIdentity, StringComparison.Ordinal))
            {
                UpdateMusicAnimationForTrack(snapshot, identity);
            }

            if (snapshot.HasTrack)
            {
                bool confirmsPendingSwitch = _showNextTrackChange &&
                    !identity.Equals(_trackSwitchInitialIdentity, StringComparison.Ordinal);
                bool automaticDisplay = trackChanged || confirmsPendingSwitch;
                if (shouldShowWhenFound || automaticDisplay)
                {
                    ShowTrackInfo(snapshot, holdAfterLeave: automaticDisplay);
                }

                if (confirmsPendingSwitch)
                {
                    ConfirmTrackSwitch("track-title");
                }

                if (trackChanged)
                {
                    _logger.Info("media.track_changed", "System media track metadata changed.");
                }
            }
            else if (shouldShowWhenFound)
            {
                ShowTrackInfoUnavailable();
            }
        }
        finally
        {
            _trackInfoRefreshInFlight = false;
            _trackInfoShowRequested = false;
        }
    }

    private async Task MonitorTrackSwitchAsync(CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset startedAt = DateTimeOffset.Now;
            bool loadingShown = false;
            while (DateTimeOffset.Now - startedAt < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(250, cancellationToken);
                if (_isClosing || !_showNextTrackChange)
                {
                    return;
                }

                await RefreshTrackInfoAsync(showWhenFound: false);
                if (!loadingShown && DateTimeOffset.Now - startedAt >= TimeSpan.FromMilliseconds(400))
                {
                    loadingShown = true;
                    await PlayReactionAsync("resonance-loading-sway", ReactionPriority.MediaOrVolume);
                }
            }

            if (_showNextTrackChange)
            {
                _showNextTrackChange = false;
                ShowFeedbackBubble("网易云没有响应，等太久了，再试一次吧");
                await PlayReactionAsync("resonance-cry-shake", ReactionPriority.MediaOrVolume);
                _logger.Info("media.track_switch_timeout", "No public playback change was observed.");
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void UpdateMusicAnimationForTrack(MediaTrackSnapshot snapshot, string identity)
    {
        _musicAnimationTrackIdentity = identity;
        string selectedAnimation = _musicAnimationSelector.SelectForArtist(snapshot.Artist);
        bool animationChanged = !string.Equals(
            selectedAnimation,
            _stateMachine.VisualState.MusicAnimationId,
            StringComparison.Ordinal);
        _stateMachine.SetMusicAnimation(selectedAnimation);
        _logger.Info(
            "media.companion_animation_selected",
            $"Animation={selectedAnimation}; ArtistClass={GetArtistClass(snapshot.Artist)}.");

        if (animationChanged &&
            _stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous &&
            _stateMachine.VisualState.ContinuousState != PetContinuousState.Dragging)
        {
            _ = TransitionToResolvedContinuousAnimationAsync(
                "animation.music_artist_transition_completed");
        }
    }

    private static string GetArtistClass(string? artist) =>
        MusicArtistMatcher.IsLuoTianyi(artist) ? "LuoTianyi" : "OtherOrUnknown";

    private void ConfirmTrackSwitch(string source)
    {
        if (!_showNextTrackChange)
        {
            return;
        }

        _showNextTrackChange = false;
        _trackSwitchCancellation?.Cancel();
        ShowFeedbackBubble("切歌成功");
        _ = PlayReactionAsync("resonance-ok", ReactionPriority.MediaOrVolume);
        _logger.Info("media.track_switch_confirmed", $"Source={source}.");
    }

    private void ShowTrackSwitchPending()
    {
        TrackTitleText.Text = "正在切换歌曲…";
        TrackArtistText.Text = "等待网易云更新歌曲信息";
        TrackArtistText.Visibility = Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(
            TrackInfoBubble,
            "正在切换歌曲，等待网易云更新歌曲信息");
        ShowTrackInfoSurface(holdAfterLeave: true);
    }

    private void ShowTrackInfo(MediaTrackSnapshot snapshot, bool holdAfterLeave)
    {
        TrackTitleText.Text = snapshot.Title;
        TrackArtistText.Text = snapshot.Artist;
        TrackArtistText.Visibility = string.IsNullOrWhiteSpace(snapshot.Artist)
            ? Visibility.Collapsed
            : Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(
            TrackInfoBubble,
            MediaTrackText.BuildAccessibleLabel(snapshot));
        ShowTrackInfoSurface(holdAfterLeave);
    }

    private void ShowTrackInfoUnavailable()
    {
        TrackTitleText.Text = "暂未读取到歌曲名称";
        TrackArtistText.Text = "请确认网易云正在播放并允许系统媒体控制";
        TrackArtistText.Visibility = Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(
            TrackInfoBubble,
            "暂未读取到歌曲名称");
        ShowTrackInfoSurface(holdAfterLeave: false);
    }

    private void ShowTrackInfoSurface(bool holdAfterLeave)
    {
        _trackInfoMotion.Show();
        _trackInfoHideTimer.Stop();
        if (holdAfterLeave && !_previewTrackInfo)
        {
            _trackInfoHideTimer.Start();
        }
    }

    private void OnTrackInfoHideTimerTick(object? sender, EventArgs e)
    {
        _trackInfoHideTimer.Stop();
        if (!_previewTrackInfo && !IsMouseOver)
        {
            _trackInfoMotion.Hide();
        }
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
            _musicPreviewOverride = true;
            _musicActivityDetector.Reset();
            StartMusicPlayback("automatic-preview", "洛天依");
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

    private async Task BeginMediaControlsPreviewAsync()
    {
        await Task.Delay(500);
        if (_isClosing)
        {
            return;
        }

        Rect workArea = SystemParameters.WorkArea;
        Top = Clamp(Top + 300, workArea.Top, workArea.Bottom - ActualHeight);
        Topmost = true;
        _mediaControlsMotion.Show();
    }

    private async Task BeginLiveTrackInfoPreviewAsync()
    {
        await Task.Delay(700);
        if (!_isClosing)
        {
            await RefreshTrackInfoAsync(showWhenFound: true);
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
        _isClosing = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _trackSwitchCancellation?.Cancel();
        _trackSwitchCancellation?.Dispose();
        _musicDetectionTimer.Stop();
        _musicDetectionTimer.Tick -= OnMusicDetectionTimerTick;
        _feedbackBubbleTimer.Stop();
        _feedbackBubbleTimer.Tick -= OnFeedbackBubbleTimerTick;
        _volumeFeedbackMergeTimer.Stop();
        _volumeFeedbackMergeTimer.Tick -= OnVolumeFeedbackMergeTimerTick;
        _systemVolumePollTimer.Stop();
        _systemVolumePollTimer.Tick -= OnSystemVolumePollTimerTick;
        _mediaControlsHideTimer.Stop();
        _mediaControlsHideTimer.Tick -= OnMediaControlsHideTimerTick;
        _trackInfoRefreshTimer.Stop();
        _trackInfoRefreshTimer.Tick -= OnTrackInfoRefreshTimerTick;
        _trackInfoHideTimer.Stop();
        _trackInfoHideTimer.Tick -= OnTrackInfoHideTimerTick;
        _idleSceneTimer.Stop();
        _idleSceneTimer.Tick -= OnIdleSceneTimerTick;
        _genshinStatusTimer.Stop();
        _genshinStatusTimer.Tick -= OnGenshinStatusTimerTick;
        _messageNotificationStatusTimer.Stop();
        _messageNotificationStatusTimer.Tick -= OnMessageNotificationStatusTimerTick;
        _singleClickTimer.Stop();
        _singleClickTimer.Tick -= OnSingleClickTimerTick;
        _pointerGesture.Cancel();
        _pettingGesture.Cancel();
        _landingBounceMotion.Cancel();
        _bodyReactionMotion.Cancel();
        _mediaControlsMotion.Cancel();
        _trackInfoMotion.Cancel();
        CancelGenshinPresentations(restoreContinuousAnimation: false);
        CancelMessageNotificationPresentation(restoreContinuousAnimation: false);
        CancelVisualTransition();
        _animationPlayer?.Dispose();
        if (_systemResumeSource is not null)
        {
            _systemResumeSource.Resumed -= OnSystemResumed;
            _systemResumeSource.Suspended -= OnSystemSuspended;
            _systemResumeSource.Dispose();
        }
        if (_protectedGameMonitor is not null)
        {
            _protectedGameMonitor.PresenceChanged -= OnProtectedGamePresenceChanged;
            _protectedGameMonitor.Dispose();
        }
        if (_messageNotificationSource is not null)
        {
            if (_messageNotificationSubscribed)
            {
                _messageNotificationSource.NotificationReceived -= OnMessageNotificationReceived;
                _messageNotificationSubscribed = false;
            }
            _messageNotificationSource.Dispose();
        }
        if (_systemVolumeService is not null)
        {
            if (_externalVolumeFeedbackSubscribed)
            {
                _systemVolumeService.VolumeChanged -= OnSystemVolumeChanged;
                _externalVolumeFeedbackSubscribed = false;
            }
            _systemVolumeService.Dispose();
        }
        if (!_persistSettings)
        {
            return;
        }

        if (_edgeDockSide != EdgeDockSide.None)
        {
            PositionEdgeDock(hidden: false);
        }

        _settings = _settings with
        {
            Window = _settings.Window with
            {
                AlwaysOnTop = _permanentTopmost,
                StartWithWindows = _startupRegistrationService?.IsEnabled ?? false,
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

    private async Task BeginEdgeDockPreviewAsync(EdgeDockSide side)
    {
        await Task.Delay(500);
        if (_isClosing)
        {
            return;
        }

        DesktopRectangle workArea = GetCurrentWorkArea();
        switch (side)
        {
            case EdgeDockSide.Left:
                Left = workArea.Left;
                break;
            case EdgeDockSide.Right:
                Left = workArea.Right - ActualWidth;
                break;
            case EdgeDockSide.Bottom:
                Top = workArea.Bottom - ActualHeight;
                break;
            default:
                return;
        }

        BeginWindowDrag();
        UpdateDragEdgePreview();
        await Task.Delay(900);
        if (!_isClosing)
        {
            EndWindowDrag();
        }
    }

    private static string[] ParseGenshinProcessNames(string? value)
    {
        string[] names = (value ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return names.Length > 0
            ? names
            : ["YuanShen.exe", "GenshinImpact.exe"];
    }

    private static double Clamp(double value, double minimum, double maximum) =>
        Math.Clamp(value, minimum, Math.Max(minimum, maximum));

    private void CancelVisualTransition()
    {
        _visualSwapTransition.Cancel();
    }
}
