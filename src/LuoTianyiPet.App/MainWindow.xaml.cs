using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
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
using WpfDataObject = System.Windows.IDataObject;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfDragEventArgs = System.Windows.DragEventArgs;

namespace LuoTianyiPet.App;

public partial class MainWindow : Window
{
    private const string CloseAnimation = "resonance-cracked-shake";
    private const string LandingAnimation = "codename-landing-bounce";
    private const string MusicPausedAnimation = PetVisualState.MusicPausedAnimation;
    private const string GenshinLaunchAnimation = "resonance-no-playing";
    private const string GenshinCameoAnimation = "resonance-please";
    private const string MessageNotificationAnimation = "codename-curious-sway";
    private const string FileDropPromptAnimation = "resonance-give-me";
    private const string FileDropFailureAnimation = "resonance-cry-shake";
    private const string CloudMusicLaunchWaitingAnimation = "resonance-loading-sway";
    private const double GenshinCameoSafeMargin = 24;
    private const double MediaControlsReservedHeight = 58;
    private const double TrackInfoReservedHeight = 52;
    private const double EdgeDockActivationDepth = 28;
    private const double EdgeDockReleaseDepth = 14;
    private const double EdgeDockDragOverscan = 96;
    private const double EdgeDockAlphaInset = 2;
    private const double BottomControlsLayoutDistance = 80;
    private const double SideDockWallClipLeftRatio = 0.9;
    private const double SideDockWallClipWidthRatio = 0.092;
    private const double SideDockRevealPlaybackRate = 1.3;
    private const double BottomDockHidePlaybackRate = 0.7;
    private const int SideDockHiddenFrame = 3;
    private const int SideDockHideStartFrame = 7;
    private const int SideDockRevealEndFrame = 19;
    private const int BottomDockHiddenFrame = 3;
    private const int BottomDockHideStartFrame = 5;
    private const int BottomDockRevealEndFrame = 7;
    private static readonly TimeSpan DoubleClickInterval = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan BodyInteractionRecoveryDelay = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan TrackInfoAutomaticDisplayDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MusicPausePresentationDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TimeGreetingPresentationDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UserPauseFastConfirmationWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan GenshinLaunchPresentationDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FileDropDwellDuration = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan CloudMusicLaunchShortcutDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CloudMusicLaunchTimeout = TimeSpan.FromSeconds(30);
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
    private readonly DownwardFlingTracker _downwardFlingTracker = new();
    private readonly PettingGestureRecognizer _pettingGesture = new(
        TimeSpan.FromMilliseconds(350),
        24,
        1,
        24);
    private BodyHitMap _bodyHitMap;
    private readonly BodyInteractionResolver _bodyInteractionResolver = new();
    private readonly MusicPlaybackAnimationSelector _musicAnimationSelector = new();
    private readonly DispatcherTimer _singleClickTimer;
    private readonly DispatcherTimer _musicDetectionTimer;
    private readonly DispatcherTimer _musicPausePresentationTimer;
    private readonly DispatcherTimer _feedbackBubbleTimer;
    private readonly DispatcherTimer _mediaControlsHideTimer;
    private readonly DispatcherTimer _trackInfoRefreshTimer;
    private readonly DispatcherTimer _trackInfoHideTimer;
    private readonly DispatcherTimer _idleSceneTimer;
    private readonly DispatcherTimer _timeSceneTimer;
    private readonly DispatcherTimer _genshinStatusTimer;
    private readonly DispatcherTimer _messageNotificationStatusTimer;
    private readonly DispatcherTimer _bunChaseTimer;
    private readonly IAudioSessionProbe? _audioSessionProbe;
    private readonly IMediaCommandSender _mediaCommandSender;
    private readonly IMediaApplicationLauncher _mediaApplicationLauncher;
    private readonly IStartupRegistrationService? _startupRegistrationService;
    private readonly IMediaTrackInfoSource? _mediaTrackInfoSource;
    private readonly IUserIdleTimeSource _userIdleTimeSource;
    private readonly ISystemResumeSource? _systemResumeSource;
    private readonly IProtectedGameProcessMonitor? _protectedGameMonitor;
    private readonly IForegroundApplicationProbe? _foregroundApplicationProbe;
    private readonly IMessageNotificationSource? _messageNotificationSource;
    private readonly IRecycleBinService _recycleBinService;
    private readonly IDesktopItemDisappearanceSource? _desktopItemDisappearanceSource;
    private readonly IWindowWorkAreaProvider _windowWorkAreaProvider;
    private readonly BirthdayEasterEggScheduler _birthdayEasterEggScheduler = new();
    private readonly SystemResumeEventGate _systemResumeEventGate = new();
    private readonly TimeSceneTransitionTracker _timeSceneTransitionTracker = new();
    private readonly GenshinBackgroundCameoScheduler _genshinCameoScheduler = new();
    private readonly RandomPetPositionSelector _randomPetPositionSelector = new();
    private readonly ProtectedGamePresenceTracker _genshinProcessMatcher;
    private readonly MessageProviderMatcher _messageProviderMatcher;
    private readonly MessageNotificationCoordinator _messageNotificationCoordinator;
    private readonly MusicAudioActivityDetector _musicActivityDetector;
    private readonly System.Windows.Input.Cursor? _petPointerCursor;
    private readonly System.Windows.Input.Cursor? _headPatCursor;
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
    private readonly bool _previewBunChase;
    private readonly bool _showQaTaskbar;
    private readonly MessageProvider? _previewMessageNotification;
    private readonly EdgeDockSide? _previewEdgeDock;
    private readonly bool _previewBottomControlsLayout;
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
    private bool _permanentTopmost;
    private CancellationTokenSource? _trackSwitchCancellation;
    private CancellationTokenSource? _cloudMusicLaunchCancellation;
    private readonly CancellationTokenSource _timeGreetingPresentationCancellation = new();
    private string _trackSwitchInitialIdentity = string.Empty;
    private string _musicAnimationTrackIdentity = string.Empty;
    private bool _trackSwitchSawAudioGap;
    private DateTimeOffset? _userPauseFastConfirmationUntil;
    private Guid? _cloudMusicLaunchReactionToken;
    private bool _cloudMusicLaunchWaiting;
    private EdgeDockSide _edgeDockSide;
    private EdgeDockSide _dragEdgeCandidate;
    private DesktopRectangle? _dragIntentPetBoundsInWindow;
    private bool _dragReleaseRequestsLanding;
    private bool _edgeDockRevealed;
    private bool _useBottomControlsLayout;
    private int _edgeDockAnimationGeneration;
    private MediaTrackSnapshot _lastTrackSnapshot = MediaTrackSnapshot.Unavailable;
    private string _lastTrackIdentity = string.Empty;
    private Point _dragPressScreenPoint;
    private double _dragStartLeft;
    private double _dragStartTop;
    private BodyRegionId? _lastDebugHitRegion;
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
    private Guid? _fileDropReactionToken;
    private bool _fileDragPresentationActive;
    private bool _fileDropTargetReady;
    private bool _fileDropInProgress;
    private DateTimeOffset? _fileDropHoverStartedAt;
    private TrayIconController? _trayIcon;
    private StartupTimeSceneDecision? _pendingTimeGreetingDecision;
    private bool _timeGreetingPresentationInFlight;
    private readonly List<BunTargetWindow> _bunTargets = [];
    private BunTargetWindow? _activeBunTarget;
    private Point? _bunReturnPosition;
    private Guid? _bunChaseReactionToken;
    private DateTimeOffset _bunLastMotionAt;
    private DateTimeOffset _bunLastSafetyCheckAt;
    private bool _bunChaseActive;
    private bool _bunReturning;
    private bool _bunEating;
    private DateTimeOffset _suppressDesktopTreatUntil;
    private DesktopToolWindowBehavior? _desktopToolWindowBehavior;

    public MainWindow(
        AppSettings settings,
        ISettingsStore settingsStore,
        IAppLogger logger,
        AnimationCatalog? animationCatalog,
        IAudioSessionProbe? audioSessionProbe,
        IMediaCommandSender mediaCommandSender,
        IMediaApplicationLauncher mediaApplicationLauncher,
        IStartupRegistrationService? startupRegistrationService,
        IMediaTrackInfoSource? mediaTrackInfoSource,
        IUserIdleTimeSource userIdleTimeSource,
        ISystemResumeSource? systemResumeSource,
        IProtectedGameProcessMonitor? protectedGameMonitor,
        IForegroundApplicationProbe? foregroundApplicationProbe,
        IMessageNotificationSource? messageNotificationSource,
        IRecycleBinService recycleBinService,
        IDesktopItemDisappearanceSource? desktopItemDisappearanceSource,
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
        bool previewBunChase,
        MessageProvider? previewMessageNotification,
        EdgeDockSide? previewEdgeDock,
        bool previewBottomControlsLayout,
        string? previewBodyReaction,
        bool showQaTaskbar,
        bool persistSettings)
    {
        _settings = settings with
        {
            Appearance = AppearancePreferences.Normalize(settings.Appearance),
        };
        _permanentTopmost = settings.Window.AlwaysOnTop;
        _settingsStore = settingsStore;
        _logger = logger;
        _animationCatalog = animationCatalog;
        _audioSessionProbe = audioSessionProbe;
        _mediaCommandSender = mediaCommandSender;
        _mediaApplicationLauncher = mediaApplicationLauncher ??
            throw new ArgumentNullException(nameof(mediaApplicationLauncher));
        _startupRegistrationService = startupRegistrationService;
        _mediaTrackInfoSource = mediaTrackInfoSource;
        _userIdleTimeSource = userIdleTimeSource;
        _systemResumeSource = systemResumeSource;
        _protectedGameMonitor = protectedGameMonitor;
        _foregroundApplicationProbe = foregroundApplicationProbe;
        _messageNotificationSource = messageNotificationSource;
        _recycleBinService = recycleBinService ?? throw new ArgumentNullException(nameof(recycleBinService));
        _desktopItemDisappearanceSource = desktopItemDisappearanceSource;
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
        string fullBodyAnimation = AppearanceOptionIds.ResolveFullBodyAnimation(
            _settings.Appearance.FullBodyStyle);
        _bodyHitMap = BodyHitMap.ForFullBodyAnimation(fullBodyAnimation);
        _stateMachine = new PetStateMachine(initialVisualState with
        {
            FullBodyAnimationId = fullBodyAnimation,
        });
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
        _previewBunChase = previewBunChase;
        _showQaTaskbar = showQaTaskbar;
        _previewMessageNotification = previewMessageNotification;
        _previewEdgeDock = previewEdgeDock;
        _previewBottomControlsLayout = previewBottomControlsLayout;
        _previewBodyReaction = previewBodyReaction;
        _persistSettings = persistSettings;
        InitializeComponent();
        if (!_showQaTaskbar)
        {
            _desktopToolWindowBehavior = new DesktopToolWindowBehavior(
                this,
                keepVisibleOnShowDesktop: true);
        }
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
        _petPointerCursor = TryLoadCursorAsset("pet-pointer.cur");
        _headPatCursor = TryLoadCursorAsset("pet-headpat.cur");
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
        _musicPausePresentationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = MusicPausePresentationDuration,
        };
        _musicPausePresentationTimer.Tick += OnMusicPausePresentationTimerTick;
        _feedbackBubbleTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2.4),
        };
        _feedbackBubbleTimer.Tick += OnFeedbackBubbleTimerTick;
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
        _timeSceneTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _timeSceneTimer.Tick += OnTimeSceneTimerTick;
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
        _bunChaseTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _bunChaseTimer.Tick += OnBunChaseTimerTick;
        ShowInTaskbar = showQaTaskbar;
        _animationPlayer = animationCatalog is null
            ? null
            : new AnimationFramePlayer(PetImage, animationCatalog);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_showQaTaskbar)
        {
            _logger.Info(
                "window.desktop_tool_mode",
                _desktopToolWindowBehavior?.IsToolWindowStyleApplied == true
                    ? "Desktop tool-window mode enabled; excluded from task switchers and protected from Show Desktop minimization."
                    : "Desktop tool-window mode requested, but native style verification did not succeed.");
        }
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

        DesktopRectangle workArea = GetCurrentWorkArea();
        double desiredLeft = _settings.Window.Left ?? workArea.Right - ActualWidth - 32;
        double desiredTop = _settings.Window.Top ?? workArea.Bottom - ActualHeight - 32;
        Left = Clamp(desiredLeft, workArea.Left, workArea.Right - ActualWidth);
        Top = Clamp(desiredTop, workArea.Top, workArea.Bottom - ActualHeight);

        PlayResolvedContinuousAnimation();
        UpdateBottomControlsLayoutForCurrentPosition();
        if (_previewBottomControlsLayout)
        {
            ApplyBottomControlsLayout(useAbovePetLayout: true);
            DesktopRectangle petBounds = GetPetImageBoundsInWindow();
            Top = workArea.Bottom - petBounds.Bottom;
        }
        if (_persistSettings)
        {
            _timeSceneTransitionTracker.Seed(TimeOnly.FromDateTime(DateTime.Now));
            _ = PlayStartupGreetingAsync(DateTimeOffset.Now);
            _timeSceneTimer.Start();
        }
        StartSystemResumeMonitoring();
        StartGenshinMonitoring();
        StartMessageNotificationMonitoring();
        if (_desktopItemDisappearanceSource is not null)
        {
            _desktopItemDisappearanceSource.ItemDisappeared += OnDesktopItemDisappeared;
            if (_settings.FileTreats.EnableDesktopFileTreats)
            {
                _desktopItemDisappearanceSource.Start();
                _logger.Info(
                    "file_treat.desktop_observer_started",
                    "Desktop disappearance observer started without retaining file paths.");
            }
        }
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
        if (_previewBunChase)
        {
            _ = BeginBunChasePreviewAsync();
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
        await PlayTimeGreetingPresentationAsync(decision, "time.startup_greeting");
    }

    private void OnTimeSceneTimerTick(object? sender, EventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        StartupTimeSceneDecision? decision = _timeSceneTransitionTracker.Observe(
            TimeOnly.FromDateTime(DateTime.Now));
        if (decision is not null)
        {
            _pendingTimeGreetingDecision = decision;
            _logger.Info("time.period_boundary_detected", decision.Scene.ToString());
        }

        if (_pendingTimeGreetingDecision is not null && !_timeGreetingPresentationInFlight)
        {
            _ = TryPlayPendingTimeGreetingAsync();
        }
    }

    private async Task TryPlayPendingTimeGreetingAsync()
    {
        if (_pendingTimeGreetingDecision is not StartupTimeSceneDecision decision ||
            _timeGreetingPresentationInFlight)
        {
            return;
        }

        _timeGreetingPresentationInFlight = true;
        try
        {
            bool accepted = await PlayTimeGreetingPresentationAsync(
                decision,
                "time.period_boundary_greeting");
            if (accepted && _pendingTimeGreetingDecision == decision)
            {
                _pendingTimeGreetingDecision = null;
            }
        }
        finally
        {
            _timeGreetingPresentationInFlight = false;
        }
    }

    private async Task<bool> PlayTimeGreetingPresentationAsync(
        StartupTimeSceneDecision decision,
        string eventName)
    {
        DateTimeOffset startedAt = DateTimeOffset.Now;
        ReactionStartOutcome outcome = _stateMachine.TryStartReaction(
            new ReactionRequest(
                decision.AnimationId,
                ReactionPriority.TimeGreeting,
                startedAt.Add(TimeGreetingPresentationDuration).AddSeconds(5)),
            startedAt);
        if (outcome.Token is not Guid token)
        {
            _logger.Info(eventName + ".deferred", outcome.Result.ToString());
            return false;
        }

        if (outcome.Result == ReactionStartResult.Replaced)
        {
            CleanupReplacedGenshinPresentation();
            CleanupReplacedMessageNotificationPresentation();
        }

        UpdateBodyHitDebugOverlay();
        bool transitioned = await _visualSwapTransition.PlayAsync(
            () => PlayAnimation(decision.AnimationId, preserveVisualTransition: true));
        if (!transitioned || _isClosing ||
            _animationPlayer?.CurrentAnimationId != decision.AnimationId)
        {
            if (_stateMachine.ActiveReactionToken == token)
            {
                _stateMachine.CancelActiveReaction();
            }
            return false;
        }

        _bodyReactionMotion.PlayFor(decision.AnimationId);
        _logger.Info(
            eventName,
            $"Scene={decision.Scene}; DurationSeconds={TimeGreetingPresentationDuration.TotalSeconds:0}.");
        try
        {
            await Task.Delay(
                TimeGreetingPresentationDuration,
                _timeGreetingPresentationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return true;
        }

        if (_isClosing || !_stateMachine.CompleteReaction(token, DateTimeOffset.Now))
        {
            return true;
        }

        _bodyReactionMotion.Cancel();
        await TransitionToResolvedContinuousAnimationAsync(eventName + ".completed");
        return true;
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

        _logger.Info("time.resume_state_restored", e.Reason.ToString());
        if (_stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous)
        {
            _ = TransitionToResolvedContinuousAnimationAsync(
                "time.resume_state_restored.transition_completed");
        }
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

        ApplyIdleScene(IdleSceneResolver.SleepThreshold);
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

        DateTimeOffset startedAt = DateTimeOffset.Now;
        ReactionStartOutcome outcome = _stateMachine.TryStartReaction(
            new ReactionRequest(
                GenshinLaunchAnimation,
                ReactionPriority.Genshin,
                startedAt.AddSeconds(10)),
            startedAt);
        if (outcome.Token is not Guid token)
        {
            if (retryOnFailure && _protectedGameMonitor?.IsRunning == true)
            {
                _pendingGenshinLaunch = true;
            }
            return;
        }

        if (outcome.Result == ReactionStartResult.Replaced)
        {
            CleanupReplacedGenshinPresentation();
            CleanupReplacedMessageNotificationPresentation();
        }

        Guid topmostToken = AcquireTransientTopmost();
        _genshinLaunchReactionToken = token;
        _genshinLaunchTopmostToken = topmostToken;
        UpdateBodyHitDebugOverlay();
        bool transitioned = await _visualSwapTransition.PlayAsync(
            () => PlayAnimation(
                GenshinLaunchAnimation,
                () => _ = CompleteGenshinLaunchAfterMinimumDurationAsync(token, startedAt),
                preserveVisualTransition: true));
        if (transitioned && !_isClosing &&
            _animationPlayer?.CurrentAnimationId == GenshinLaunchAnimation)
        {
            _logger.Info(
                "genshin.launch_reaction_started",
                "One loop started; the final frame will be held for a five-second total without activation.");
            return;
        }

        if (_stateMachine.ActiveReactionToken == token)
        {
            _stateMachine.CancelActiveReaction();
        }
        FinishGenshinPresentation(token);
        if (retryOnFailure && _protectedGameMonitor?.IsRunning == true)
        {
            _pendingGenshinLaunch = true;
        }
    }

    private async Task CompleteGenshinLaunchAfterMinimumDurationAsync(
        Guid token,
        DateTimeOffset startedAt)
    {
        TimeSpan remaining = GenshinLaunchPresentationDuration - (DateTimeOffset.Now - startedAt);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }

        if (_isClosing || _genshinLaunchReactionToken != token ||
            _stateMachine.ActiveReactionToken != token)
        {
            return;
        }

        CompleteReaction(token, suppressBodyAfter: false);
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
            e.Notification,
            sourceIsForeground,
            canShow);
        _logger.Info("notification.signal_processed", decision.ToString());
        if (decision == MessageNotificationDecision.Show)
        {
            _ = BeginMessageNotificationAsync(e.Notification);
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
                out MessageNotificationSummary pendingNotification))
        {
            _ = BeginMessageNotificationAsync(pendingNotification);
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

    private async Task BeginMessageNotificationAsync(MessageNotificationSummary notification)
    {
        if (_isClosing || _messageNotificationReactionToken is not null)
        {
            _messageNotificationCoordinator.QueuePending(notification);
            return;
        }

        Guid topmostToken = AcquireTransientTopmost();
        Guid? reactionToken = await PlayReactionAsync(
            MessageNotificationAnimation,
            ReactionPriority.Notification);
        if (reactionToken is not Guid token)
        {
            ReleaseTransientTopmost(topmostToken);
            _messageNotificationCoordinator.QueuePending(notification);
            return;
        }

        _messageNotificationReactionToken = token;
        _messageNotificationTopmostToken = topmostToken;
        _activeMessageProvider = notification.Provider;
        ShowMessageNotification(notification);
        _logger.Info(
            "notification.reaction_started",
            notification.ConversationDisplayName is null
                ? "Source category and application icon were shown without message content."
                : "Source category, application icon, and conversation title were shown without message content.");
    }

    private async Task BeginMessageNotificationPreviewAsync(MessageProvider provider)
    {
        await Task.Delay(700);
        if (!_isClosing)
        {
            await BeginMessageNotificationAsync(new MessageNotificationSummary(
                provider,
                DateTimeOffset.Now,
                "测试联系人"));
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
        if (_isClosing)
        {
            return;
        }

        UpdatePetCursor(ToPointerPoint(e.GetPosition(this)));
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        PointerPoint position = ToPointerPoint(e.GetPosition(this));
        if (_pettingGesture.IsTracking)
        {
            HandlePettingMove(position, now);
        }
        else if (!_pettingGestureConsumedPress)
        {
            HandlePointerAction(_pointerGesture.Move(position));
        }

        if (_isWindowDragging)
        {
            MoveWindowWithPointer(GetPointerScreenPositionInDips(e), now);
        }

        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _isClosing)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        Point releaseScreenPoint = GetPointerScreenPositionInDips(e);
        if (_isWindowDragging)
        {
            _dragReleaseRequestsLanding = _downwardFlingTracker.Complete(
                ToPointerPoint(releaseScreenPoint),
                now);
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
                now));
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
        _downwardFlingTracker.Cancel();
        _dragReleaseRequestsLanding = false;
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
        PetPlaybackPlan currentPlan = _stateMachine.Resolve(DateTimeOffset.Now);
        if (_stateMachine.VisualState.ContinuousState == PetContinuousState.MusicPaused &&
            currentPlan.Source == PlaybackPlanSource.Continuous &&
            currentPlan.AnimationId == MusicPausedAnimation)
        {
            DismissMusicPausePresentation("double-click");
            return;
        }

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
            PetImage.Clip = null;
            PetImage.IsHitTestVisible = true;
        }

        _dragEdgeCandidate = EdgeDockSide.None;

        if (!_stateMachine.BeginDrag())
        {
            _downwardFlingTracker.Cancel();
            return;
        }

        _isWindowDragging = true;
        _dragReleaseRequestsLanding = false;
        _downwardFlingTracker.Begin(ToPointerPoint(_dragPressScreenPoint), DateTimeOffset.Now);
        PlayCurrentDragVisual();

        _dragStartLeft = Left;
        _dragStartTop = Top;
        _dragIntentPetBoundsInWindow = GetPetImageBoundsInWindow();

        UpdateBodyHitDebugOverlay();
        _logger.Info("interaction.drag_started", _stateMachine.VisualState.SelectedDisplayMode.ToString());
    }

    private void MoveWindowWithPointer(Point currentScreenPoint, DateTimeOffset observedAt)
    {
        _downwardFlingTracker.Add(ToPointerPoint(currentScreenPoint), observedAt);
        double desiredLeft = _dragStartLeft + currentScreenPoint.X - _dragPressScreenPoint.X;
        double desiredTop = _dragStartTop + currentScreenPoint.Y - _dragPressScreenPoint.Y;
        Left = Clamp(
            desiredLeft,
            SystemParameters.VirtualScreenLeft - EdgeDockDragOverscan,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - ActualWidth +
                EdgeDockDragOverscan);
        Top = Clamp(
            desiredTop,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - ActualHeight +
                EdgeDockDragOverscan);
        UpdateBottomControlsLayoutForCurrentPosition();
        if (_dragEdgeCandidate == EdgeDockSide.None)
        {
            _dragIntentPetBoundsInWindow = GetPetImageBoundsInWindow();
        }
        UpdateDragEdgePreview();
    }

    private void EndWindowDrag()
    {
        if (!_isWindowDragging)
        {
            return;
        }

        bool playLandingFeedback = _dragReleaseRequestsLanding;
        _dragReleaseRequestsLanding = false;
        _downwardFlingTracker.Cancel();
        _isWindowDragging = false;
        if (_stateMachine.EndDrag())
        {
            if (TryEnterEdgeDock())
            {
                _dragIntentPetBoundsInWindow = null;
                _logger.Info("interaction.drag_ended", "Pet docked at a screen edge.");
                return;
            }

            _dragIntentPetBoundsInWindow = null;
            _dragEdgeCandidate = EdgeDockSide.None;
            SetEdgeMirror(false);
            SnapVisiblePetInsideWorkArea();
            UpdateBottomControlsLayoutForCurrentPosition();

            if (_stateMachine.VisualState.ContinuousState == PetContinuousState.MusicPlaying)
            {
                RestoreAfterMusicDrag();
                _logger.Info("interaction.drag_ended", "Music animation continued without landing feedback.");
            }
            else if (_stateMachine.VisualState.SelectedDisplayMode == PetDisplayMode.Compact)
            {
                if (playLandingFeedback)
                {
                    PlayLandingFeedback();
                    _logger.Info(
                        "interaction.drag_ended",
                        "Fast downward compact fling requested landing feedback.");
                }
                else
                {
                    RestoreAfterCompactDrag();
                    _logger.Info(
                        "interaction.drag_ended",
                        "Compact drag restored without routine landing feedback.");
                }
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
        _musicPausePresentationTimer.Stop();
        _userPauseFastConfirmationUntil = null;
        DateTimeOffset now = DateTimeOffset.Now;
        string artist = artistOverride ?? _lastTrackSnapshot.Artist;
        string selectedAnimation = _musicAnimationSelector.SelectForArtist(artist);
        _musicAnimationTrackIdentity = artistOverride is null
            ? _lastTrackIdentity
            : "preview-luo-tianyi";
        _stateMachine.SetMusicAnimation(selectedAnimation);
        _stateMachine.SetContinuousState(PetContinuousState.MusicPlaying);
        bool completedApplicationLaunchWait = _cloudMusicLaunchWaiting;
        if (completedApplicationLaunchWait)
        {
            FinishCloudMusicLaunchWait(restoreContinuousAnimation: false);
            ShowFeedbackBubble("网易云已开始播放");
        }

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
        _stateMachine.SetContinuousState(PetContinuousState.MusicPaused);
        _musicPausePresentationTimer.Stop();
        _musicPausePresentationTimer.Start();
        _userPauseFastConfirmationUntil = null;
        UpdatePlayPauseGlyph();
        if (_stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous &&
            _stateMachine.VisualState.ContinuousState != PetContinuousState.Dragging)
        {
            _ = TransitionToResolvedContinuousAnimationAsync("animation.music_stopped_transition_completed");
        }

        _logger.Info(
            "media.playback_paused",
            $"Source={source}; Pause presentation started for {MusicPausePresentationDuration.TotalSeconds:0} seconds.");
    }

    private void OnMusicPausePresentationTimerTick(object? sender, EventArgs e)
    {
        DismissMusicPausePresentation("timeout");
    }

    private void DismissMusicPausePresentation(string source)
    {
        _musicPausePresentationTimer.Stop();
        if (_stateMachine.VisualState.ContinuousState != PetContinuousState.MusicPaused)
        {
            return;
        }

        _stateMachine.SetContinuousState(PetContinuousState.Idle);
        UpdatePlayPauseGlyph();
        if (_stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous &&
            _stateMachine.VisualState.ContinuousState != PetContinuousState.Dragging)
        {
            _ = TransitionToResolvedContinuousAnimationAsync(
                "animation.music_pause_dismissed_transition_completed");
        }

        _logger.Info("media.pause_presentation_dismissed", $"Source={source}.");
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

        DateTimeOffset now = DateTimeOffset.Now;
        MusicActivityTransition transition = _musicActivityDetector.Update(snapshot, now);
        if (transition == MusicActivityTransition.None &&
            _userPauseFastConfirmationUntil is DateTimeOffset confirmationUntil)
        {
            if (now <= confirmationUntil)
            {
                transition = _musicActivityDetector.ConfirmStoppedAfterUserPause(snapshot);
                if (transition == MusicActivityTransition.Stopped)
                {
                    _logger.Info(
                        "media.user_pause_confirmed_fast",
                        "The first silent Core Audio sample confirmed the user's pause command.");
                }
            }
            else
            {
                _userPauseFastConfirmationUntil = null;
            }
        }

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
            _userPauseFastConfirmationUntil = null;
            if (_showNextTrackChange)
            {
                _trackSwitchSawAudioGap = true;
            }

            StopMusicPlayback("core-audio");
        }
    }

    private void PlayCurrentDragVisual()
    {
        string? dragAnimation = _stateMachine.Resolve(DateTimeOffset.Now).AnimationId;
        if (dragAnimation == PetVisualState.CompactDraggingAnimation && _animationCatalog is not null)
        {
            AnimationAssetManifest manifest = _animationCatalog.GetRequired(dragAnimation);
            PlayAnimationRange(
                dragAnimation,
                startFrameIndex: 0,
                endFrameIndex: manifest.FrameDurationsMilliseconds.Count - 1);
            return;
        }

        if (_animationPlayer?.CurrentAnimationId != dragAnimation)
        {
            PlayResolvedContinuousAnimation();
        }
    }

    private void RestoreAfterCompactDrag() =>
        _ = TransitionToResolvedContinuousAnimationAsync("animation.compact_drag_restored");

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
            PlayCurrentDragVisual();
            return;
        }

        SetEdgeMirror(candidate == EdgeDockSide.Left);
        ShowEdgeDockHideStartFrame(candidate);
        _logger.Info("interaction.drag_edge_preview", candidate.ToString());
    }

    private EdgeDockSide ResolveCurrentEdgeDockSide()
    {
        DesktopRectangle workArea = GetCurrentWorkArea();
        DesktopRectangle localPet = _dragIntentPetBoundsInWindow ?? GetPetImageBoundsInWindow();
        DesktopRectangle stablePet = new(
            Left + localPet.Left,
            Top + localPet.Top,
            localPet.Width,
            localPet.Height);
        return EdgeDockResolver.ResolveHideIntentWithHysteresis(
            stablePet,
            workArea,
            EdgeDockActivationDepth,
            EdgeDockReleaseDepth,
            _dragEdgeCandidate);
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
        if (decision.RestoredFromSleep)
        {
            _logger.Info("animation.long_idle_wake", $"Restored={decision.TargetState}.");
            if (_stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous)
            {
                _ = TransitionToResolvedContinuousAnimationAsync(
                    "animation.long_idle_wake.transition_completed");
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
            ApplyAnimationManifest(manifest);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or ArgumentException or KeyNotFoundException or NotSupportedException)
        {
            _logger.Error("animation.play_failed", exception);
            ShowFallback("Animation playback failed.");
        }
    }

    private void PlayAnimationRange(
        string animationId,
        int startFrameIndex,
        int endFrameIndex,
        Action? completed = null,
        double playbackRate = 1.0)
    {
        _landingBounceMotion.Cancel();
        _bodyReactionMotion.Cancel();
        CancelVisualTransition();
        if (_animationPlayer is null || _animationCatalog is null)
        {
            ShowFallback("Animation catalog unavailable.");
            return;
        }

        try
        {
            AnimationAssetManifest manifest = _animationPlayer.PlayRange(
                animationId,
                startFrameIndex,
                endFrameIndex,
                completed,
                playbackRate);
            ApplyAnimationManifest(manifest);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or ArgumentException or
            KeyNotFoundException or NotSupportedException)
        {
            _logger.Error("animation.range_play_failed", exception);
            ShowFallback("Animation range playback failed.");
        }
    }

    private void ShowAnimationFrame(string animationId, int frameIndex)
    {
        _landingBounceMotion.Cancel();
        _bodyReactionMotion.Cancel();
        CancelVisualTransition();
        if (_animationPlayer is null || _animationCatalog is null)
        {
            ShowFallback("Animation catalog unavailable.");
            return;
        }

        try
        {
            AnimationAssetManifest manifest = _animationPlayer.ShowFrame(animationId, frameIndex);
            ApplyAnimationManifest(manifest);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or ArgumentException or
            KeyNotFoundException or NotSupportedException)
        {
            _logger.Error("animation.frame_show_failed", exception);
            ShowFallback("Animation frame could not be shown.");
        }
    }

    private void ApplyAnimationManifest(AnimationAssetManifest manifest)
    {
        double displayScale = _settings.Appearance.DisplayScalePercent / 100.0;
        double displayWidth = manifest.DisplayWidth * displayScale;
        double displayHeight = manifest.DisplayHeight * displayScale;
        PetImage.Width = displayWidth;
        PetImage.Height = displayHeight;
        PetImage.Visibility = Visibility.Visible;
        PetImage.IsHitTestVisible = true;
        FallbackSurface.Visibility = Visibility.Collapsed;
        ResizeAroundBottomCenter(
            displayWidth + 16,
            displayHeight + 16 + MediaControlsReservedHeight + TrackInfoReservedHeight);
        UpdateBodyHitDebugOverlay();
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
        double bottomOverflow = _useBottomControlsLayout ? PetVisual.Margin.Bottom : 0;
        Top = Clamp(bottom - height, workArea.Top, workArea.Bottom - height + bottomOverflow);
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
        if (side == EdgeDockSide.Bottom)
        {
            ApplyBottomControlsLayout(useAbovePetLayout: true);
        }

        int generation = ++_edgeDockAnimationGeneration;
        _stateMachine.CancelActiveReaction();
        _mediaControlsMotion.Hide(animate: false);
        _trackInfoMotion.Hide(animate: false);
        SetEdgeMirror(side == EdgeDockSide.Left);
        EdgeDockHandle.Visibility = Visibility.Collapsed;
        PlayEdgeDockToward(
            revealed: false,
            () =>
            {
                if (generation == _edgeDockAnimationGeneration && !_edgeDockRevealed)
                {
                    PositionEdgeDock(hidden: true);
                }
            });
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
        EdgeDockHandle.Visibility = Visibility.Collapsed;
        PetImage.IsHitTestVisible = true;
        PlayEdgeDockToward(revealed: true);
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
        EdgeDockHandle.Visibility = Visibility.Collapsed;
        PlayEdgeDockToward(
            revealed: false,
            () =>
            {
                if (generation == _edgeDockAnimationGeneration && !_edgeDockRevealed)
                {
                    PositionEdgeDock(hidden: true);
                }
            });
        PositionEdgeDock(hidden: false);
        _logger.Info("window.edge_dock_hidden", _edgeDockSide.ToString());
    }

    private void PlayEdgeDockToward(bool revealed, Action? completed = null)
    {
        if (_edgeDockSide == EdgeDockSide.None)
        {
            return;
        }

        string animationId = GetEdgeDockAnimation(_edgeDockSide);
        (int hiddenFrame, int hideStartFrame, int revealEndFrame) =
            GetEdgeDockFrames(_edgeDockSide);
        int? currentFrame = _animationPlayer?.CurrentAnimationId == animationId
            ? _animationPlayer.CurrentFrameIndex
            : null;
        EdgeDockFrameRoute route = EdgeDockFrameRouteResolver.Resolve(
            revealed,
            currentFrame,
            hiddenFrame,
            hideStartFrame,
            revealEndFrame);

        PlayAnimationRange(
            animationId,
            route.StartFrameIndex,
            route.EndFrameIndex,
            completed,
            GetEdgeDockPlaybackRate(_edgeDockSide, revealed));
    }

    private void ShowEdgeDockHideStartFrame(EdgeDockSide side)
    {
        (_, int hideStartFrame, _) = GetEdgeDockFrames(side);
        ShowAnimationFrame(GetEdgeDockAnimation(side), hideStartFrame);
    }

    private void PositionEdgeDock(bool hidden)
    {
        if (_edgeDockSide == EdgeDockSide.None)
        {
            return;
        }

        ConfigureEdgeDockHandle(hidden);
        UpdateLayout();
        DesktopRectangle workArea = GetCurrentWorkArea();
        DesktopRectangle localPet = hidden
            ? GetPetImageVisibleBoundsInWindow()
            : GetPetImageBoundsInWindow();
        switch (_edgeDockSide)
        {
            case EdgeDockSide.Left:
                Left = workArea.Left - localPet.Left - EdgeDockAlphaInset;
                Top = Clamp(Top, workArea.Top - localPet.Top, workArea.Bottom - localPet.Bottom);
                break;
            case EdgeDockSide.Right:
                Left = workArea.Right - localPet.Right + EdgeDockAlphaInset;
                Top = Clamp(Top, workArea.Top - localPet.Top, workArea.Bottom - localPet.Bottom);
                break;
            case EdgeDockSide.Bottom:
                Left = Clamp(Left, workArea.Left - localPet.Left, workArea.Right - localPet.Right);
                Top = workArea.Bottom - localPet.Bottom + EdgeDockAlphaInset;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ConfigureEdgeDockHandle(bool hidden)
    {
        if (!hidden || _edgeDockSide == EdgeDockSide.None)
        {
            EdgeDockHandle.Visibility = Visibility.Collapsed;
            PetImage.Clip = null;
            PetImage.IsHitTestVisible = true;
            return;
        }

        PetImage.IsHitTestVisible = false;
        double petWidth = Math.Max(1, PetImage.ActualWidth);
        double petHeight = Math.Max(1, PetImage.ActualHeight);
        if (_edgeDockSide == EdgeDockSide.Bottom)
        {
            PetImage.Clip = new RectangleGeometry(new Rect(
                petWidth * 0.20,
                petHeight * 0.86,
                petWidth * 0.60,
                petHeight * 0.14));
        }
        else
        {
            PetImage.Clip = new RectangleGeometry(new Rect(
                petWidth * SideDockWallClipLeftRatio,
                0,
                petWidth * SideDockWallClipWidthRatio,
                petHeight));
        }

        UpdateLayout();
        DesktopRectangle handleBounds = GetPetImageVisibleBoundsInWindow();
        EdgeDockHandle.Width = handleBounds.Width;
        EdgeDockHandle.Height = handleBounds.Height;
        EdgeDockHandle.HorizontalAlignment = WpfHorizontalAlignment.Left;
        EdgeDockHandle.VerticalAlignment = VerticalAlignment.Top;
        EdgeDockHandle.Margin = new Thickness(
            handleBounds.Left,
            handleBounds.Top,
            0,
            0);
        EdgeDockHandle.Visibility = Visibility.Visible;
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

    private static (int HiddenFrame, int HideStartFrame, int RevealEndFrame) GetEdgeDockFrames(
        EdgeDockSide side) => side switch
    {
        EdgeDockSide.Left or EdgeDockSide.Right =>
            (SideDockHiddenFrame, SideDockHideStartFrame, SideDockRevealEndFrame),
        EdgeDockSide.Bottom =>
            (BottomDockHiddenFrame, BottomDockHideStartFrame, BottomDockRevealEndFrame),
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    private static double GetEdgeDockPlaybackRate(EdgeDockSide side, bool revealed) =>
        (side, revealed) switch
        {
            (EdgeDockSide.Left or EdgeDockSide.Right, true) => SideDockRevealPlaybackRate,
            (EdgeDockSide.Bottom, false) => BottomDockHidePlaybackRate,
            _ => 1.0,
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
                () => _settings.Appearance.DisplayScalePercent,
                percent => Dispatcher.BeginInvoke(() => SetDisplayScalePercent(percent, save: false)),
                percent => Dispatcher.BeginInvoke(() => SetDisplayScalePercent(percent, save: true)),
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
        HideMessageNotification();
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
        HideMessageNotification();
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
        UpdateBottomControlsLayoutForCurrentPosition();
    }

    private void UpdateBottomControlsLayoutForCurrentPosition()
    {
        if (!IsLoaded || PetImage.ActualHeight <= 0)
        {
            return;
        }

        DesktopRectangle workArea = GetCurrentWorkArea();
        bool useAbovePetLayout = _edgeDockSide == EdgeDockSide.Bottom ||
            EdgeDockResolver.IsNearBottom(
                GetPetImageDesktopBounds(),
                workArea,
                BottomControlsLayoutDistance);
        ApplyBottomControlsLayout(useAbovePetLayout);
    }

    private void ApplyBottomControlsLayout(bool useAbovePetLayout)
    {
        if (_useBottomControlsLayout == useAbovePetLayout)
        {
            return;
        }

        UpdateLayout();
        double petBottomBefore = Top + GetPetImageBoundsInWindow().Bottom;
        _useBottomControlsLayout = useAbovePetLayout;
        if (useAbovePetLayout)
        {
            PetVisual.Margin = new Thickness(8, 118, 8, 8);
            MusicTransitionFlash.Margin = new Thickness(8, 118, 8, 8);
            MediaControls.VerticalAlignment = VerticalAlignment.Top;
            MediaControls.Margin = new Thickness(0, 7, 0, 0);
            TrackInfoBubble.Margin = new Thickness(5, 60, 5, 0);
            FeedbackBubble.Margin = new Thickness(6, 110, 6, 6);
        }
        else
        {
            PetVisual.Margin = new Thickness(8, 60, 8, 66);
            MusicTransitionFlash.Margin = new Thickness(8, 60, 8, 66);
            MediaControls.VerticalAlignment = VerticalAlignment.Bottom;
            MediaControls.Margin = new Thickness(0, 0, 0, 7);
            TrackInfoBubble.Margin = new Thickness(5, 6, 5, 0);
            FeedbackBubble.Margin = new Thickness(6, 56, 6, 6);
        }

        UpdateLayout();
        double petBottomAfter = Top + GetPetImageBoundsInWindow().Bottom;
        double topAdjustment = petBottomBefore - petBottomAfter;
        Top += topAdjustment;
        if (_isWindowDragging)
        {
            _dragStartTop += topAdjustment;
        }

        _logger.Info(
            "window.bottom_controls_layout_changed",
            useAbovePetLayout ? "AbovePet" : "BelowPet");
    }

    private void SnapVisiblePetInsideWorkArea()
    {
        DesktopRectangle workArea = GetCurrentWorkArea();
        DesktopRectangle pet = GetPetImageDesktopBounds();
        if (pet.Left < workArea.Left)
        {
            Left += workArea.Left - pet.Left;
        }
        else if (pet.Right > workArea.Right)
        {
            Left -= pet.Right - workArea.Right;
        }

        pet = GetPetImageDesktopBounds();
        if (pet.Top < workArea.Top)
        {
            Top += workArea.Top - pet.Top;
        }
        else if (pet.Bottom > workArea.Bottom)
        {
            Top -= pet.Bottom - workArea.Bottom;
        }
    }

    private DesktopRectangle GetPetImageDesktopBounds()
    {
        DesktopRectangle local = GetPetImageBoundsInWindow();
        return new DesktopRectangle(
            Left + local.Left,
            Top + local.Top,
            local.Width,
            local.Height);
    }

    private DesktopRectangle GetPetImageBoundsInWindow()
    {
        UpdateLayout();
        double width = PetImage.ActualWidth > 0 ? PetImage.ActualWidth : PetImage.Width;
        double height = PetImage.ActualHeight > 0 ? PetImage.ActualHeight : PetImage.Height;
        Rect transformed = PetImage
            .TransformToAncestor(this)
            .TransformBounds(new Rect(0, 0, width, height));
        return new DesktopRectangle(
            transformed.Left,
            transformed.Top,
            transformed.Width,
            transformed.Height);
    }

    private DesktopRectangle GetPetImageVisibleBoundsInWindow()
    {
        UpdateLayout();
        Rect sourceBounds = PetImage.Clip?.Bounds ?? new Rect(
            0,
            0,
            PetImage.ActualWidth > 0 ? PetImage.ActualWidth : PetImage.Width,
            PetImage.ActualHeight > 0 ? PetImage.ActualHeight : PetImage.Height);
        Rect transformed = PetImage
            .TransformToAncestor(this)
            .TransformBounds(sourceBounds);
        return new DesktopRectangle(
            transformed.Left,
            transformed.Top,
            transformed.Width,
            transformed.Height);
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
            _settings.Notifications,
            _settings.Window,
            _settings.FileTreats,
            _settings.Appearance,
            _startupRegistrationService?.IsEnabled ?? false,
            _messageNotificationSource,
            _animationCatalog)
        {
            Owner = this,
        };
        if (settingsWindow.ShowDialog() == true)
        {
            ApplyMessageNotificationPreferences(settingsWindow.SelectedNotificationPreferences);
            ApplyFileTreatPreferences(settingsWindow.SelectedFileTreatPreferences);
            ApplyAppearancePreferences(settingsWindow.SelectedAppearancePreferences);
            ApplyWindowPreferences(
                settingsWindow.SelectedWindowPreferences,
                settingsWindow.StartWithWindowsSelected);
        }
    }

    private void ApplyAppearancePreferences(AppearancePreferences preferences)
    {
        AppearancePreferences normalized = AppearancePreferences.Normalize(preferences);
        string previousFullBodyAnimation = _stateMachine.VisualState.FullBodyAnimationId;
        int previousScale = _settings.Appearance.DisplayScalePercent;
        _settings = _settings with { Appearance = normalized };

        string fullBodyAnimation = AppearanceOptionIds.ResolveFullBodyAnimation(
            normalized.FullBodyStyle);
        _stateMachine.SetFullBodyAnimation(fullBodyAnimation);
        _bodyHitMap = BodyHitMap.ForFullBodyAnimation(fullBodyAnimation);

        bool appearanceChanged = !string.Equals(
            previousFullBodyAnimation,
            fullBodyAnimation,
            StringComparison.Ordinal);
        bool scaleChanged = previousScale != normalized.DisplayScalePercent;
        if (appearanceChanged &&
            _stateMachine.VisualState.SelectedDisplayMode == PetDisplayMode.FullBodyInteractive &&
            _stateMachine.Resolve(DateTimeOffset.Now).Source == PlaybackPlanSource.Continuous)
        {
            _ = TransitionToResolvedContinuousAnimationAsync("settings.appearance_changed");
        }
        else if (scaleChanged)
        {
            ApplyCurrentDisplayScale();
        }

        UpdateBodyHitDebugOverlay();
        _trayIcon?.RefreshChecks();
        _logger.Info(
            "settings.appearance_applied",
            $"FullBodyStyle={normalized.FullBodyStyle}; BunEatingStyle={normalized.BunEatingStyle}; ScalePercent={normalized.DisplayScalePercent}.");
        if (_persistSettings)
        {
            _ = SaveSettingsAsync("settings.appearance_saved", "Appearance preferences saved.");
        }
    }

    private void SetDisplayScalePercent(int percent, bool save)
    {
        int normalized = Math.Clamp(
            percent,
            AppearancePreferences.MinimumDisplayScalePercent,
            AppearancePreferences.MaximumDisplayScalePercent);
        if (_settings.Appearance.DisplayScalePercent == normalized)
        {
            if (save && _persistSettings)
            {
                _ = SaveSettingsAsync("settings.display_scale_saved", "Display scale preference saved.");
            }
            return;
        }

        _settings = _settings with
        {
            Appearance = _settings.Appearance with { DisplayScalePercent = normalized },
        };
        ApplyCurrentDisplayScale();
        _logger.Info("window.display_scale_changed", $"ScalePercent={normalized}.");
        if (save && _persistSettings)
        {
            _ = SaveSettingsAsync("settings.display_scale_saved", "Display scale preference saved.");
        }
    }

    private void ApplyCurrentDisplayScale()
    {
        if (_animationCatalog is null || _animationPlayer?.CurrentAnimationId is not string animationId)
        {
            return;
        }

        try
        {
            ApplyAnimationManifest(_animationCatalog.GetRequired(animationId));
            if (_edgeDockSide != EdgeDockSide.None)
            {
                PositionEdgeDock(hidden: !_edgeDockRevealed);
            }
            else
            {
                SnapVisiblePetInsideWorkArea();
                UpdateBottomControlsLayoutForCurrentPosition();
            }
        }
        catch (KeyNotFoundException)
        {
            // The frame player owns the normal missing-asset fallback path.
        }
    }

    private void ApplyFileTreatPreferences(FileTreatPreferences preferences)
    {
        bool wasEnabled = _settings.FileTreats.EnableDesktopFileTreats;
        _settings = _settings with { FileTreats = preferences };
        if (_desktopItemDisappearanceSource is not null)
        {
            if (preferences.EnableDesktopFileTreats)
            {
                _desktopItemDisappearanceSource.Start();
            }
            else
            {
                _desktopItemDisappearanceSource.Stop();
                CancelBunChase(restorePosition: true, restoreContinuousAnimation: true);
            }
        }

        if (wasEnabled != preferences.EnableDesktopFileTreats)
        {
            _logger.Info(
                "file_treat.preferences_applied",
                preferences.EnableDesktopFileTreats ? "Enabled." : "Disabled.");
        }

        if (_persistSettings)
        {
            _ = SaveSettingsAsync("settings.file_treat_saved", "File treat preferences saved.");
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
        HandleTogglePlayPauseRequest();

    private void OnNextTrackClick(object sender, RoutedEventArgs e) =>
        TrySendMediaCommand(MediaCommand.NextTrack);

    private void OnRootMouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isClosing)
        {
            UpdatePetCursor(ToPointerPoint(e.GetPosition(this)));
        }

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
        PetImage.Cursor = null;
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
        if (_cloudMusicLaunchWaiting)
        {
            ShowPersistentFeedbackBubble("网易云正在启动，等音乐响起后就会自动切换");
            return;
        }

        bool userRequestedPause = command == MediaCommand.TogglePlayPause &&
            _stateMachine.VisualState.ContinuousState == PetContinuousState.MusicPlaying;
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

        if (result.WasSent && userRequestedPause)
        {
            _userPauseFastConfirmationUntil = DateTimeOffset.Now + UserPauseFastConfirmationWindow;
        }

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

    private void UpdatePetCursor(PointerPoint windowPoint)
    {
        PointerPoint? normalizedPoint = NormalizeToPetImage(windowPoint);
        bool isOpaquePixel = normalizedPoint is PointerPoint point && IsOpaquePetPixel(point);
        PetPlaybackPlan plan = _stateMachine.Resolve(DateTimeOffset.Now);
        BodyRegionId? region = isOpaquePixel && plan.BodyRegionInteractionsEnabled
            ? _bodyHitMap.HitTest(normalizedPoint!.Value)
            : null;
        PetImage.Cursor = PetCursorResolver.Resolve(
            isOpaquePixel,
            plan.BodyRegionInteractionsEnabled,
            region) switch
        {
            PetCursorKind.HeadPat => _headPatCursor ?? System.Windows.Input.Cursors.Hand,
            PetCursorKind.Interaction => _petPointerCursor ?? System.Windows.Input.Cursors.Hand,
            _ => null,
        };
    }

    private System.Windows.Input.Cursor? TryLoadCursorAsset(string fileName)
    {
        string path = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "assets",
            "cursors",
            fileName);
        try
        {
            return File.Exists(path) ? new System.Windows.Input.Cursor(path) : null;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or System.ComponentModel.Win32Exception or
            System.Security.SecurityException)
        {
            _logger.Info("cursor.asset_unavailable", $"Asset={fileName}; Error={exception.GetType().Name}.");
            return null;
        }
    }

    private void HandleTogglePlayPauseRequest()
    {
        if (_cloudMusicLaunchWaiting)
        {
            ShowPersistentFeedbackBubble("网易云正在启动，等音乐响起后就会自动切换");
            return;
        }

        MediaApplicationLaunchResult launchResult =
            _mediaApplicationLauncher.TryLaunch(_musicTargetProcessName);
        _logger.Info("media.application_launch_result", $"Status={launchResult.Status}.");
        if (launchResult.Status == MediaApplicationLaunchStatus.AlreadyRunning)
        {
            TrySendMediaCommand(MediaCommand.TogglePlayPause);
            return;
        }

        if (launchResult.Status == MediaApplicationLaunchStatus.Started)
        {
            BeginCloudMusicLaunchWait();
            return;
        }

        string message = launchResult.Status switch
        {
            MediaApplicationLaunchStatus.NotFound =>
                "没有找到网易云音乐，请先确认已经安装",
            MediaApplicationLaunchStatus.ProtectedApplicationForeground =>
                "游戏安全模式：这次没有打开网易云",
            MediaApplicationLaunchStatus.ForegroundCheckUnavailable =>
                "暂时无法确认前台程序，没有打开网易云",
            MediaApplicationLaunchStatus.SystemRejected =>
                "Windows 没能打开网易云音乐，请稍后再试",
            _ => "网易云音乐暂时无法启动",
        };
        ShowFeedbackBubble(message);
        _ = PlayBodyReactionAsync(FileDropFailureAnimation);
    }

    private void BeginCloudMusicLaunchWait()
    {
        _cloudMusicLaunchCancellation?.Cancel();
        _cloudMusicLaunchCancellation?.Dispose();
        _cloudMusicLaunchCancellation = new CancellationTokenSource();
        _cloudMusicLaunchWaiting = true;
        DateTimeOffset now = DateTimeOffset.Now;
        ReactionStartOutcome outcome = _stateMachine.TryStartReaction(
            new ReactionRequest(
                CloudMusicLaunchWaitingAnimation,
                ReactionPriority.UserInteraction,
                now + CloudMusicLaunchTimeout + TimeSpan.FromSeconds(2),
                "media:cloudmusic-launch"),
            now);
        if (outcome.Token is Guid token)
        {
            if (outcome.Result == ReactionStartResult.Replaced)
            {
                CleanupReplacedGenshinPresentation();
                CleanupReplacedMessageNotificationPresentation();
            }

            _cloudMusicLaunchReactionToken = token;
            _ = _visualSwapTransition.PlayAsync(
                () => PlayAnimation(
                    CloudMusicLaunchWaitingAnimation,
                    preserveVisualTransition: true));
        }
        else
        {
            _logger.Info("media.application_launch_animation_skipped", outcome.Result.ToString());
        }

        ShowPersistentFeedbackBubble("正在打开网易云音乐，请稍等…");
        _ = MonitorCloudMusicLaunchAsync(_cloudMusicLaunchCancellation.Token);
    }

    private async Task MonitorCloudMusicLaunchAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = DateTimeOffset.Now;
        bool playCommandSent = false;
        try
        {
            while (DateTimeOffset.Now - startedAt < CloudMusicLaunchTimeout)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                if (!_cloudMusicLaunchWaiting ||
                    _stateMachine.VisualState.ContinuousState == PetContinuousState.MusicPlaying)
                {
                    return;
                }

                if (!playCommandSent &&
                    DateTimeOffset.Now - startedAt >= CloudMusicLaunchShortcutDelay &&
                    _mediaApplicationLauncher.IsRunning(_musicTargetProcessName))
                {
                    MediaCommandSendResult playResult = _mediaCommandSender.TrySend(
                        MediaCommand.TogglePlayPause,
                        DateTimeOffset.Now);
                    _logger.Info(
                        "media.application_launch_play_result",
                        $"Status={playResult.Status}.");
                    if (playResult.WasSent)
                    {
                        playCommandSent = true;
                        if (_audioSessionProbe is null)
                        {
                            FinishCloudMusicLaunchWait(restoreContinuousAnimation: true);
                            ShowFeedbackBubble(
                                "网易云已打开并发送播放快捷键；音乐检测关闭，无法确认播放状态");
                            return;
                        }

                        ShowPersistentFeedbackBubble("网易云已打开，正在等待音乐开始播放…");
                    }
                    else if (playResult.Status is not
                        (MediaCommandSendStatus.RateLimited or MediaCommandSendStatus.KeyboardBusy))
                    {
                        FinishCloudMusicLaunchWait(restoreContinuousAnimation: true);
                        ShowFeedbackBubble(GetMediaCommandFailureMessage(playResult.Status));
                        await PlayBodyReactionAsync(FileDropFailureAnimation);
                        return;
                    }
                }
            }

            if (_cloudMusicLaunchWaiting)
            {
                FinishCloudMusicLaunchWait(restoreContinuousAnimation: true);
                ShowFeedbackBubble(playCommandSent
                    ? "等待网易云播放超时，请打开网易云检查歌曲"
                    : "网易云启动超时，请稍后再试");
                await PlayBodyReactionAsync(FileDropFailureAnimation);
            }
        }
        catch (OperationCanceledException)
        {
            // Playback detection or window shutdown owns the final state.
        }
    }

    private void FinishCloudMusicLaunchWait(bool restoreContinuousAnimation)
    {
        if (!_cloudMusicLaunchWaiting)
        {
            return;
        }

        _cloudMusicLaunchWaiting = false;
        _cloudMusicLaunchCancellation?.Cancel();
        _cloudMusicLaunchCancellation?.Dispose();
        _cloudMusicLaunchCancellation = null;
        Guid? token = _cloudMusicLaunchReactionToken;
        _cloudMusicLaunchReactionToken = null;
        bool completed = token is Guid reactionToken &&
            _stateMachine.CompleteReaction(reactionToken, DateTimeOffset.Now);
        if (completed && restoreContinuousAnimation && !_isClosing)
        {
            _ = TransitionToResolvedContinuousAnimationAsync(
                "media.application_launch_wait_completed");
        }
    }

    private static string GetMediaCommandFailureMessage(MediaCommandSendStatus status) => status switch
    {
        MediaCommandSendStatus.Disabled => "网易云快捷键控制尚未启用",
        MediaCommandSendStatus.InvalidShortcut => "播放快捷键设置无效，请检查配置",
        MediaCommandSendStatus.ProtectedApplicationForeground =>
            "游戏安全模式：这次没有发送播放快捷键",
        MediaCommandSendStatus.ForegroundCheckUnavailable =>
            "暂时无法确认前台程序，没有发送播放快捷键",
        MediaCommandSendStatus.KeyboardBusy => "键盘正在使用，请松开按键后再试",
        MediaCommandSendStatus.RateLimited => "操作太快啦，请稍等一下",
        MediaCommandSendStatus.SystemRejected => "系统没有接受播放快捷键，请再试一次",
        _ => "没有发送播放快捷键",
    };

    private void ShowFeedbackBubble(string message)
    {
        FeedbackBubbleText.Text = message;
        FeedbackBubble.Visibility = Visibility.Visible;
        _feedbackBubbleTimer.Stop();
        _feedbackBubbleTimer.Start();
    }

    private void ShowPersistentFeedbackBubble(string message)
    {
        FeedbackBubbleText.Text = message;
        FeedbackBubble.Visibility = Visibility.Visible;
        _feedbackBubbleTimer.Stop();
    }

    private void OnDesktopItemDisappeared(object? sender, DesktopItemDisappearedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing || DateTimeOffset.Now < _suppressDesktopTreatUntil ||
                !IsBunChaseEnvironmentSafe())
            {
                _logger.Info(
                    "file_treat.desktop_event_suppressed",
                    "Desktop disappearance event was suppressed by the foreground safety policy.");
                return;
            }

            Point target = ConvertScreenPixelsToDips(e.ScreenPositionPixels);
            QueueBunTreat(target);
            _logger.Info(
                "file_treat.bun_created",
                e.UsedCachedIconPosition
                    ? "Bun created at the cached desktop icon position."
                    : "Bun created at the safe cursor-position fallback.");
        });
    }

    private Point ConvertScreenPixelsToDips(PointerPoint point)
    {
        PresentationSource? source = PresentationSource.FromVisual(this);
        Matrix fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return fromDevice.Transform(new Point(point.X, point.Y));
    }

    private void QueueBunTreat(Point screenPosition)
    {
        int maximum = Math.Clamp(_settings.FileTreats.MaximumQueuedBuns, 1, 12);
        if (_bunTargets.Count >= maximum)
        {
            ShowFeedbackBubble("包子太多啦，先吃完这些吧");
            return;
        }

        string bunPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "assets",
            "objects",
            "xiaolongbao.png");
        if (!File.Exists(bunPath))
        {
            _logger.Info("file_treat.bun_asset_missing", "The runtime bun image is unavailable.");
            return;
        }

        BunTargetWindow bun = new(bunPath)
        {
            Left = screenPosition.X - 32,
            Top = screenPosition.Y - 32,
        };
        _bunTargets.Add(bun);
        bun.Show();
        ShowFeedbackBubble("发现小笼包！拖动它，天依也会追过去");
        if (!_bunChaseActive)
        {
            BeginBunChase();
        }
    }

    private void BeginBunChase()
    {
        if (_bunTargets.Count == 0 || _isClosing ||
            (!_previewBunChase && !IsBunChaseEnvironmentSafe()))
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        (string runAnimation, _) = GetSelectedBunAnimations();
        ReactionStartOutcome outcome = _stateMachine.TryStartReaction(
            new ReactionRequest(
                runAnimation,
                ReactionPriority.UserInteraction,
                now.AddMinutes(10),
                InterruptibleByDrag: false),
            now);
        if (outcome.Token is not Guid token)
        {
            _ = RetryBunChaseAsync();
            return;
        }

        _bunChaseReactionToken = token;
        _bunChaseActive = true;
        _bunReturning = false;
        _bunEating = false;
        _bunReturnPosition ??= new Point(Left, Top);
        SelectNearestBun();
        _bunLastMotionAt = now;
        PlayAnimation(runAnimation);
        _bunChaseTimer.Start();
    }

    private async Task RetryBunChaseAsync()
    {
        await Task.Delay(900);
        if (!_bunChaseActive && _bunTargets.Count > 0 && !_isClosing)
        {
            BeginBunChase();
        }
    }

    private void SelectNearestBun()
    {
        Point petCentre = GetPetScreenCentre();
        _activeBunTarget = _bunTargets
            .OrderBy(target =>
            {
                Point centre = target.ScreenCenter;
                double dx = centre.X - petCentre.X;
                double dy = centre.Y - petCentre.Y;
                return dx * dx + dy * dy;
            })
            .FirstOrDefault();
    }

    private void OnBunChaseTimerTick(object? sender, EventArgs e)
    {
        if (!_bunChaseActive || _bunEating)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        if (!_previewBunChase && now - _bunLastSafetyCheckAt >= TimeSpan.FromMilliseconds(500))
        {
            _bunLastSafetyCheckAt = now;
            if (!IsBunChaseEnvironmentSafe())
            {
                _logger.Info(
                    "file_treat.cancelled_for_foreground_safety",
                    "Bun chase cancelled because the foreground safety check failed.");
                CancelBunChase(restorePosition: true, restoreContinuousAnimation: true);
                return;
            }
        }

        TimeSpan elapsed = now - _bunLastMotionAt;
        _bunLastMotionAt = now;
        if (_bunReturning)
        {
            if (_bunReturnPosition is not Point returnPosition)
            {
                FinishBunChase();
                return;
            }

            PointerPoint current = new(Left, Top);
            BunChaseStep step = BunChasePlanner.Advance(
                current,
                new PointerPoint(returnPosition.X, returnPosition.Y),
                330,
                elapsed,
                3);
            PetDirectionTransform.ScaleX = returnPosition.X < Left ? -1 : 1;
            Left = step.Position.X;
            Top = step.Position.Y;
            if (step.Arrived)
            {
                FinishBunChase();
            }
            return;
        }

        if (_activeBunTarget is null)
        {
            SelectNearestBun();
            if (_activeBunTarget is null)
            {
                BeginBunReturn();
                return;
            }
        }

        Point petCentre = GetPetScreenCentre();
        Point targetCentre = _activeBunTarget.ScreenCenter;
        BunChaseStep chase = BunChasePlanner.Advance(
            new PointerPoint(petCentre.X, petCentre.Y),
            new PointerPoint(targetCentre.X, targetCentre.Y),
            360,
            elapsed,
            42);
        double moveX = chase.Position.X - petCentre.X;
        double moveY = chase.Position.Y - petCentre.Y;
        PetDirectionTransform.ScaleX = targetCentre.X < petCentre.X ? -1 : 1;
        Left += moveX;
        Top += moveY;
        UpdateBottomControlsLayoutForCurrentPosition();
        if (chase.Arrived && !_activeBunTarget.IsBeingDragged)
        {
            _bunChaseTimer.Stop();
            _ = EatActiveBunAsync(_activeBunTarget);
        }
    }

    private Point GetPetScreenCentre()
    {
        DesktopRectangle bounds = GetPetImageBoundsInWindow();
        return new Point(Left + bounds.Left + bounds.Width / 2, Top + bounds.Top + bounds.Height / 2);
    }

    private async Task EatActiveBunAsync(BunTargetWindow bun)
    {
        if (_isClosing || !_bunTargets.Contains(bun))
        {
            return;
        }

        _bunEating = true;
        (string runAnimation, string eatAnimation) = GetSelectedBunAnimations();
        PlayAnimation(eatAnimation);
        await Task.Delay(420);
        DesktopRectangle bounds = GetPetImageBoundsInWindow();
        PointerPoint mouthTarget = ResolveSelectedBunMouthTarget(bounds);
        Point mouth = new(mouthTarget.X, mouthTarget.Y);
        await bun.FlyIntoAsync(mouth, TimeSpan.FromMilliseconds(680));
        bun.Close();
        _bunTargets.Remove(bun);
        _activeBunTarget = null;
        await Task.Delay(1650);
        _bunEating = false;
        if (_bunTargets.Count > 0)
        {
            SelectNearestBun();
            PlayAnimation(runAnimation);
            _bunLastMotionAt = DateTimeOffset.Now;
            _bunChaseTimer.Start();
            return;
        }

        BeginBunReturn();
    }

    private void BeginBunReturn()
    {
        _bunReturning = true;
        _activeBunTarget = null;
        PlayAnimation(GetSelectedBunAnimations().RunAnimation);
        _bunLastMotionAt = DateTimeOffset.Now;
        _bunChaseTimer.Start();
    }

    private (string RunAnimation, string EatAnimation) GetSelectedBunAnimations() =>
        AppearanceOptionIds.ResolveBunAnimations(_settings.Appearance.BunEatingStyle);

    private PointerPoint ResolveSelectedBunMouthTarget(DesktopRectangle imageBounds)
    {
        bool mirrored = PetDirectionTransform.ScaleX < 0;
        if (_settings.Appearance.BunEatingStyle == AppearanceOptionIds.BunEatingNew)
        {
            return new PointerPoint(
                Left + imageBounds.Left + imageBounds.Width * 0.50,
                Top + imageBounds.Top + imageBounds.Height * 0.30);
        }

        return BunChasePlanner.ResolveMouthTarget(
            new PointerPoint(Left + imageBounds.Left, Top + imageBounds.Top),
            imageBounds.Width,
            imageBounds.Height,
            mirrored);
    }

    private void FinishBunChase()
    {
        _bunChaseTimer.Stop();
        _bunReturning = false;
        _bunEating = false;
        _bunChaseActive = false;
        _activeBunTarget = null;
        _bunReturnPosition = null;
        PetDirectionTransform.ScaleX = 1;
        if (_bunChaseReactionToken is Guid token)
        {
            _stateMachine.CompleteReaction(token, DateTimeOffset.Now);
        }
        _bunChaseReactionToken = null;
        _ = TransitionToResolvedContinuousAnimationAsync("file_treat.completed");
    }

    private void CancelBunChase(bool restorePosition, bool restoreContinuousAnimation)
    {
        _bunChaseTimer.Stop();
        foreach (BunTargetWindow bun in _bunTargets.ToArray())
        {
            bun.Close();
        }
        _bunTargets.Clear();
        if (restorePosition && _bunReturnPosition is Point position)
        {
            Left = position.X;
            Top = position.Y;
        }
        _bunChaseActive = false;
        _bunReturning = false;
        _bunEating = false;
        _activeBunTarget = null;
        _bunReturnPosition = null;
        PetDirectionTransform.ScaleX = 1;
        if (_bunChaseReactionToken is Guid token)
        {
            _stateMachine.CompleteReaction(token, DateTimeOffset.Now);
        }
        _bunChaseReactionToken = null;
        if (restoreContinuousAnimation && !_isClosing)
        {
            _ = TransitionToResolvedContinuousAnimationAsync("file_treat.cancelled");
        }
    }

    private void OnFileDragEnter(object sender, WpfDragEventArgs e) =>
        UpdateFileDragTarget(e);

    private void OnFileDragOver(object sender, WpfDragEventArgs e) =>
        UpdateFileDragTarget(e);

    private void OnFileDragLeave(object sender, WpfDragEventArgs e)
    {
        if (!_fileDropInProgress)
        {
            FinishFileDragPresentation(restoreContinuousAnimation: true);
        }

        e.Handled = true;
    }

    private async void OnFileDrop(object sender, WpfDragEventArgs e)
    {
        e.Handled = true;
        if (_fileDropInProgress || !IsFileDropReady(e) ||
            !TryGetDroppedPaths(e.Data, out string[] paths))
        {
            e.Effects = WpfDragDropEffects.None;
            FinishFileDragPresentation(restoreContinuousAnimation: true);
            return;
        }

        e.Effects = WpfDragDropEffects.Move;
        if ((paths.Length > 10 || paths.Any(Directory.Exists)) &&
            MessageBox.Show(
                this,
                paths.Any(Directory.Exists)
                    ? $"将 {paths.Length} 个项目（其中包含文件夹）放入 Windows 回收站吗？"
                    : $"一次将 {paths.Length} 个项目放入 Windows 回收站吗？",
                "确认放入回收站",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            FinishFileDragPresentation(restoreContinuousAnimation: true);
            ShowFeedbackBubble("已取消，文件仍在原处");
            _logger.Info("file_drop.cancelled", $"Count={paths.Length}; Confirmation declined.");
            return;
        }

        _fileDropInProgress = true;
        _suppressDesktopTreatUntil = DateTimeOffset.Now.AddSeconds(3);
        ShowFeedbackBubble("正在放入 Windows 回收站…");
        RecycleBinOperationResult result;
        try
        {
            nint ownerHandle = new WindowInteropHelper(this).Handle;
            result = await _recycleBinService.MoveToRecycleBinAsync(paths, ownerHandle);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or COMException)
        {
            result = new RecycleBinOperationResult(
                RecycleBinOperationStatus.Failed,
                paths.Length,
                0,
                exception.Message);
        }
        finally
        {
            _fileDropInProgress = false;
        }

        FinishFileDragPresentation(restoreContinuousAnimation: false);
        if (result.Succeeded)
        {
            ShowFeedbackBubble(result.RecycledCount == 1
                ? "已放进回收站，需要时可以恢复"
                : $"已将 {result.RecycledCount} 个项目放进回收站");
            _logger.Info(
                "file_drop.recycled",
                $"Requested={result.RequestedCount}; Recycled={result.RecycledCount}.");
            QueueBunTreat(GetPetScreenCentre());
            return;
        }

        _suppressDesktopTreatUntil = DateTimeOffset.Now;

        string failureMessage = result.Status switch
        {
            RecycleBinOperationStatus.Cancelled => "已取消，文件仍在原处",
            RecycleBinOperationStatus.PartialFailure =>
                $"只有 {result.RecycledCount} 个项目进入回收站，请检查其余文件",
            RecycleBinOperationStatus.Rejected => result.Message,
            _ => "没有放进回收站，文件仍在原处",
        };
        ShowFeedbackBubble(failureMessage);
        _logger.Info(
            "file_drop.failed",
            $"Status={result.Status}; Requested={result.RequestedCount}; Recycled={result.RecycledCount}.");
        await PlayReactionAsync(FileDropFailureAnimation, ReactionPriority.UserInteraction);
    }

    private void UpdateFileDragTarget(WpfDragEventArgs e)
    {
        bool supported = !_fileDropInProgress &&
            IsSupportedFileDrop(e, requirePetHit: false) &&
            IsFileDropEnvironmentSafe();
        if (supported)
        {
            StartFileDragPresentation();
        }

        bool overPet = supported && IsSupportedFileDrop(e, requirePetHit: true);
        _fileDropHoverStartedAt = overPet
            ? _fileDropHoverStartedAt ?? DateTimeOffset.Now
            : null;
        bool accepted = overPet && IsFileDropReady(e);
        e.Effects = accepted ? WpfDragDropEffects.Move : WpfDragDropEffects.None;
        e.Handled = true;
        if (accepted && !_fileDropTargetReady)
        {
            _fileDropTargetReady = true;
            ShowFeedbackBubble("松手即可放入回收站");
        }
        else if (supported && !accepted && _fileDropTargetReady)
        {
            _fileDropTargetReady = false;
            ShowFeedbackBubble("把文件放到我身上，停一下再松手");
        }
        else if (!supported && !_fileDropInProgress)
        {
            FinishFileDragPresentation(restoreContinuousAnimation: true);
        }
    }

    private bool IsFileDropReady(WpfDragEventArgs e) =>
        IsSupportedFileDrop(e, requirePetHit: true) &&
        IsFileDropEnvironmentSafe() &&
        _fileDropHoverStartedAt is DateTimeOffset hoverStartedAt &&
        DateTimeOffset.Now - hoverStartedAt >= FileDropDwellDuration;

    private bool IsFileDropEnvironmentSafe()
    {
        if (_isClosing || _systemSessionUnavailable || _edgeDockSide != EdgeDockSide.None ||
            _isWindowDragging || _foregroundApplicationProbe is null)
        {
            return false;
        }

        ForegroundApplicationSnapshot foreground = _foregroundApplicationProbe.Query();
        return foreground.Succeeded &&
            !foreground.IsFullscreen &&
            !_genshinProcessMatcher.IsTargetProcess(foreground.ProcessName);
    }

    private bool IsBunChaseEnvironmentSafe()
    {
        if (_isClosing || _systemSessionUnavailable || _edgeDockSide != EdgeDockSide.None ||
            _isWindowDragging || _foregroundApplicationProbe is null)
        {
            return false;
        }

        ForegroundApplicationSnapshot foreground = _foregroundApplicationProbe.Query();
        return DesktopFileTreatSafety.AllowsForeground(
            foreground,
            _genshinProcessMatcher.IsTargetProcess(foreground.ProcessName));
    }

    private bool IsSupportedFileDrop(WpfDragEventArgs e, bool requirePetHit)
    {
        if (!e.Data.GetDataPresent(WpfDataFormats.FileDrop, autoConvert: false))
        {
            return false;
        }

        if (!requirePetHit)
        {
            return true;
        }

        Point position = e.GetPosition(this);
        DesktopRectangle bounds = _edgeDockSide == EdgeDockSide.None
            ? GetPetImageBoundsInWindow()
            : GetPetImageVisibleBoundsInWindow();
        return position.X >= bounds.Left && position.X <= bounds.Right &&
            position.Y >= bounds.Top && position.Y <= bounds.Bottom;
    }

    private static bool TryGetDroppedPaths(WpfDataObject data, out string[] paths)
    {
        paths = [];
        if (!data.GetDataPresent(WpfDataFormats.FileDrop, autoConvert: false) ||
            data.GetData(WpfDataFormats.FileDrop, autoConvert: false) is not string[] rawPaths)
        {
            return false;
        }

        try
        {
            paths = rawPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(System.IO.Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return paths.Length > 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private void StartFileDragPresentation()
    {
        if (_fileDragPresentationActive || _isClosing)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        ReactionStartOutcome outcome = _stateMachine.TryStartReaction(
            new ReactionRequest(
                FileDropPromptAnimation,
                ReactionPriority.UserInteraction,
                now.AddMinutes(10),
                InterruptibleByDrag: false),
            now);
        if (outcome.Token is not Guid token)
        {
            _logger.Info("file_drop.prompt_skipped", outcome.Result.ToString());
            return;
        }

        if (outcome.Result == ReactionStartResult.Replaced)
        {
            CleanupReplacedGenshinPresentation();
            CleanupReplacedMessageNotificationPresentation();
        }

        _fileDropReactionToken = token;
        _fileDragPresentationActive = true;
        PlayAnimation(
            FileDropPromptAnimation,
            () => _logger.Info(
                "file_drop.prompt_held",
                "Give-me animation completed and is holding its final frame."));
        ShowFeedbackBubble("把文件放到我身上，停一下再松手");
        _logger.Info("file_drop.prompt_started", "A local file drag entered the pet target.");
    }

    private void FinishFileDragPresentation(bool restoreContinuousAnimation)
    {
        if (!_fileDragPresentationActive)
        {
            return;
        }

        _fileDragPresentationActive = false;
        _fileDropTargetReady = false;
        _fileDropHoverStartedAt = null;
        Guid? token = _fileDropReactionToken;
        _fileDropReactionToken = null;
        bool completed = token is Guid reactionToken &&
            _stateMachine.CompleteReaction(reactionToken, DateTimeOffset.Now);
        if (completed && restoreContinuousAnimation && !_isClosing)
        {
            _ = TransitionToResolvedContinuousAnimationAsync("file_drop.prompt_cancelled");
        }
    }

    private void ShowMessageNotification(MessageNotificationSummary notification)
    {
        string providerName = MessageProviderMatcher.GetDisplayName(notification.Provider);
        ImageSource? applicationIcon = DecodeNotificationImage(notification.ApplicationIcon);
        ImageSource? contactAvatar = DecodeNotificationImage(notification.ContactAvatar);
        ImageSource? primaryIcon = contactAvatar ?? applicationIcon;

        MessageSourceText.Text = $"{providerName} · 新消息";
        MessageConversationText.Text = string.IsNullOrWhiteSpace(notification.ConversationDisplayName)
            ? "有新消息"
            : notification.ConversationDisplayName;
        MessageProviderGlyph.Text = notification.Provider == MessageProvider.Qq ? "Q" : "微";
        MessagePrimaryIconSurface.Background = notification.Provider == MessageProvider.Qq
            ? new SolidColorBrush(Color.FromRgb(0x39, 0xA9, 0xF2))
            : new SolidColorBrush(Color.FromRgb(0x20, 0xC0, 0x5C));
        MessagePrimaryIcon.Source = primaryIcon;
        MessageProviderGlyph.Visibility = primaryIcon is null
            ? Visibility.Visible
            : Visibility.Collapsed;

        bool showApplicationBadge = contactAvatar is not null && applicationIcon is not null;
        MessageAppBadgeIcon.Source = showApplicationBadge ? applicationIcon : null;
        MessageAppBadge.Visibility = showApplicationBadge
            ? Visibility.Visible
            : Visibility.Collapsed;
        AutomationProperties.SetName(
            MessageNotificationBubble,
            string.IsNullOrWhiteSpace(notification.ConversationDisplayName)
                ? $"{providerName} 有新消息"
                : $"{providerName}，{notification.ConversationDisplayName} 有新消息");
        MessageNotificationBubble.Visibility = Visibility.Visible;
    }

    private void HideMessageNotification()
    {
        MessageNotificationBubble.Visibility = Visibility.Collapsed;
        MessagePrimaryIcon.Source = null;
        MessageAppBadgeIcon.Source = null;
        MessageAppBadge.Visibility = Visibility.Collapsed;
    }

    private static ImageSource? DecodeNotificationImage(ReadOnlyMemory<byte>? encodedImage)
    {
        if (encodedImage is not ReadOnlyMemory<byte> bytes || bytes.IsEmpty)
        {
            return null;
        }

        try
        {
            using MemoryStream stream = new(bytes.ToArray(), writable: false);
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            // Notification icons come from another app through Windows. Malformed or
            // unsupported image data must fall back to the provider glyph.
            return null;
        }
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
            while (DateTimeOffset.Now - startedAt < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(250, cancellationToken);
                if (_isClosing || !_showNextTrackChange)
                {
                    return;
                }

                await RefreshTrackInfoAsync(showWhenFound: false);
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
        await Task.Delay(1600);
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

    private async Task BeginBunChasePreviewAsync()
    {
        await Task.Delay(700);
        if (_isClosing)
        {
            return;
        }

        DesktopRectangle workArea = GetCurrentWorkArea();
        QueueBunTreat(new Point(
            Math.Max(workArea.Left + 90, Left - 320),
            Math.Max(workArea.Top + 90, Top - 90)));
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
        _desktopToolWindowBehavior?.Dispose();
        _desktopToolWindowBehavior = null;
        _isClosing = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _trackSwitchCancellation?.Cancel();
        _trackSwitchCancellation?.Dispose();
        _cloudMusicLaunchCancellation?.Cancel();
        _cloudMusicLaunchCancellation?.Dispose();
        _cloudMusicLaunchCancellation = null;
        _musicDetectionTimer.Stop();
        _musicDetectionTimer.Tick -= OnMusicDetectionTimerTick;
        _musicPausePresentationTimer.Stop();
        _musicPausePresentationTimer.Tick -= OnMusicPausePresentationTimerTick;
        _feedbackBubbleTimer.Stop();
        _feedbackBubbleTimer.Tick -= OnFeedbackBubbleTimerTick;
        _mediaControlsHideTimer.Stop();
        _mediaControlsHideTimer.Tick -= OnMediaControlsHideTimerTick;
        _trackInfoRefreshTimer.Stop();
        _trackInfoRefreshTimer.Tick -= OnTrackInfoRefreshTimerTick;
        _trackInfoHideTimer.Stop();
        _trackInfoHideTimer.Tick -= OnTrackInfoHideTimerTick;
        _idleSceneTimer.Stop();
        _idleSceneTimer.Tick -= OnIdleSceneTimerTick;
        _timeSceneTimer.Stop();
        _timeSceneTimer.Tick -= OnTimeSceneTimerTick;
        _timeGreetingPresentationCancellation.Cancel();
        _timeGreetingPresentationCancellation.Dispose();
        _genshinStatusTimer.Stop();
        _genshinStatusTimer.Tick -= OnGenshinStatusTimerTick;
        _messageNotificationStatusTimer.Stop();
        _messageNotificationStatusTimer.Tick -= OnMessageNotificationStatusTimerTick;
        _bunChaseTimer.Stop();
        _bunChaseTimer.Tick -= OnBunChaseTimerTick;
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
        CancelBunChase(restorePosition: false, restoreContinuousAnimation: false);
        CancelVisualTransition();
        _animationPlayer?.Dispose();
        _petPointerCursor?.Dispose();
        _headPatCursor?.Dispose();
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
        if (_desktopItemDisappearanceSource is not null)
        {
            _desktopItemDisappearanceSource.ItemDisappeared -= OnDesktopItemDisappeared;
            _desktopItemDisappearanceSource.Dispose();
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

        BeginWindowDrag();
        DesktopRectangle workArea = GetCurrentWorkArea();
        DesktopRectangle localPet = GetPetImageBoundsInWindow();
        double qaOvershoot = EdgeDockActivationDepth + 4;
        switch (side)
        {
            case EdgeDockSide.Left:
                Left = workArea.Left - localPet.Left - qaOvershoot;
                break;
            case EdgeDockSide.Right:
                Left = workArea.Right - localPet.Right + qaOvershoot;
                break;
            case EdgeDockSide.Bottom:
                Top = workArea.Bottom - localPet.Bottom + qaOvershoot;
                break;
            default:
                return;
        }

        UpdateDragEdgePreview();
        double[] boundaryOffsets = [1, -1, 2, -2, 1, -1];
        foreach (double offset in boundaryOffsets)
        {
            await Task.Delay(90);
            switch (side)
            {
                case EdgeDockSide.Left:
                    Left = workArea.Left - localPet.Left - EdgeDockActivationDepth - offset;
                    break;
                case EdgeDockSide.Right:
                    Left = workArea.Right - localPet.Right + EdgeDockActivationDepth + offset;
                    break;
                case EdgeDockSide.Bottom:
                    Top = workArea.Bottom - localPet.Bottom + EdgeDockActivationDepth + offset;
                    break;
            }

            UpdateDragEdgePreview();
        }

        await Task.Delay(500);
        if (!_isClosing)
        {
            EndWindowDrag();
        }

        await Task.Delay(1200);
        if (!_isClosing && _edgeDockSide == side)
        {
            RevealEdgeDock();
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
