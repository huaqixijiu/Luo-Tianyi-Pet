using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using LuoTianyiPet.Animation;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class App : Application
{
    private const string ApplicationId = "LuoTianyiPet.App";

    private SingleInstanceGuard? _singleInstance;
    private IAppLogger? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool isPreviewOrQaRun = e.Args.Any(argument =>
            argument.StartsWith("--preview-", StringComparison.OrdinalIgnoreCase) ||
            argument.StartsWith("--qa-", StringComparison.OrdinalIgnoreCase));
        string instanceId = isPreviewOrQaRun ? $"{ApplicationId}.QA" : ApplicationId;
        _singleInstance = SingleInstanceGuard.Acquire(instanceId);
        if (!_singleInstance.IsPrimaryInstance)
        {
            Shutdown();
            return;
        }

        bool isPortable = e.Args.Contains("--portable", StringComparer.OrdinalIgnoreCase);
        LocalAppPaths paths = isPortable
            ? LocalAppPaths.CreatePortable(AppContext.BaseDirectory)
            : new LocalAppPaths();
        _logger = new FileAppLogger(paths);
        JsonSettingsStore settingsStore = new(paths);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            _logger.Info("app.storage_mode", isPortable ? "Portable." : "Installed.");
            AppSettings settings = await settingsStore.LoadAsync();
            bool simulateMissingAssets = e.Args.Contains("--qa-missing-assets", StringComparer.OrdinalIgnoreCase);
            AnimationCatalog? animationCatalog = simulateMissingAssets ? null : LoadAnimationCatalog(_logger);
            if (simulateMissingAssets)
            {
                _logger.Info("animation.catalog_qa_missing", "QA fallback mode enabled.");
            }
            PetVisualState initialVisualState = GetInitialVisualState(e.Args);
            bool previewExit = e.Args.Contains("--preview-exit", StringComparer.OrdinalIgnoreCase);
            bool previewMusicTransition = e.Args.Contains(
                "--preview-music-transition",
                StringComparer.OrdinalIgnoreCase);
            bool previewBodyHitDebug = e.Args.Contains(
                "--preview-body-hit-debug",
                StringComparer.OrdinalIgnoreCase);
            bool previewDragCycle = e.Args.Contains(
                "--preview-drag-cycle",
                StringComparer.OrdinalIgnoreCase);
            bool previewBottomControlsLayout = e.Args.Contains(
                "--qa-bottom-controls",
                StringComparer.OrdinalIgnoreCase);
            bool previewMediaControls = e.Args.Contains(
                "--qa-media-controls",
                StringComparer.OrdinalIgnoreCase) || e.Args.Contains(
                    "--qa-shortcut-menu",
                    StringComparer.OrdinalIgnoreCase) || previewBottomControlsLayout;
            bool previewTrackInfo = e.Args.Contains(
                "--qa-track-info",
                StringComparer.OrdinalIgnoreCase) || previewBottomControlsLayout;
            bool liveTrackInfoQa = e.Args.Contains(
                "--qa-track-info-live",
                StringComparer.OrdinalIgnoreCase);
            bool liveSystemVolumeQa = e.Args.Contains(
                "--qa-system-volume",
                StringComparer.OrdinalIgnoreCase);
            bool previewSettings = e.Args.Contains(
                "--qa-settings",
                StringComparer.OrdinalIgnoreCase);
            bool previewTray = e.Args.Contains(
                "--qa-tray",
                StringComparer.OrdinalIgnoreCase);
            bool previewSystemResume = e.Args.Contains(
                "--qa-system-resume",
                StringComparer.OrdinalIgnoreCase);
            bool previewLongIdle = e.Args.Contains(
                "--qa-long-idle",
                StringComparer.OrdinalIgnoreCase);
            bool previewGenshinLaunch = e.Args.Contains(
                "--qa-genshin-launch",
                StringComparer.OrdinalIgnoreCase);
            bool previewGenshinCameo = e.Args.Contains(
                "--qa-genshin-cameo",
                StringComparer.OrdinalIgnoreCase);
            MessageProvider? previewMessageNotification = ParseMessageNotificationPreview(e.Args);
            EdgeDockSide? previewEdgeDock = ParseEdgeDockPreview(e.Args);
            string? previewBodyReaction = e.Args
                .FirstOrDefault(argument => argument.StartsWith(
                    "--preview-body-reaction=",
                    StringComparison.OrdinalIgnoreCase))?
                .Split('=', 2)[1];
            bool showQaTaskbar = e.Args.Contains("--qa-window", StringComparer.OrdinalIgnoreCase);
            IAudioSessionProbe? audioSessionProbe = settings.Media.EnableCloudMusicDetection &&
                !isPreviewOrQaRun
                    ? new CoreAudioSessionProbe()
                    : null;
            Win32ShortcutInputBackend mediaInputBackend = new();
            IMediaCommandSender mediaCommandSender = new WindowsMediaCommandSender(
                mediaInputBackend,
                settings.Media,
                settings.Safety);
            IMediaApplicationLauncher mediaApplicationLauncher = new WindowsMediaApplicationLauncher(
                mediaInputBackend,
                settings.Safety);
            ISystemVolumeService? systemVolumeService = !isPreviewOrQaRun || liveSystemVolumeQa
                ? CreateSystemVolumeService(settings, _logger)
                : null;
            IStartupRegistrationService? startupRegistrationService = !isPreviewOrQaRun &&
                Environment.ProcessPath is string executablePath
                    ? new WindowsStartupRegistrationService(
                        executablePath,
                        isPortable,
                        TryGetPackageFamilyName())
                    : null;
            IMediaTrackInfoSource? mediaTrackInfoSource = !isPreviewOrQaRun || liveTrackInfoQa
                ? new SystemMediaTrackInfoSource()
                : null;
            ISystemResumeSource? systemResumeSource = !isPreviewOrQaRun
                ? new WindowsSystemResumeSource()
                : null;
            string[] genshinProcessNames = (settings.Genshin.ProcessNames ?? string.Empty)
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            IProtectedGameProcessMonitor? protectedGameMonitor =
                settings.Genshin.EnableIntegration && !isPreviewOrQaRun && genshinProcessNames.Length > 0
                    ? new PollingProtectedGameProcessMonitor(
                        genshinProcessNames,
                        TimeSpan.FromMilliseconds(
                            settings.Genshin.StatusPollIntervalMilliseconds > 0
                                ? settings.Genshin.StatusPollIntervalMilliseconds
                                : GenshinPreferences.DefaultStatusPollIntervalMilliseconds))
                    : null;
            IForegroundApplicationProbe? foregroundApplicationProbe = !isPreviewOrQaRun
                ? new WindowsForegroundApplicationProbe()
                : null;
            MessageProviderMatcher messageProviderMatcher = new(settings.Notifications);
            IMessageNotificationSource? messageNotificationSource = !isPreviewOrQaRun || previewSettings
                ? new WindowsMessageNotificationSource(messageProviderMatcher)
                : null;
            MainWindow window = new(
                settings,
                settingsStore,
                _logger,
                animationCatalog,
                audioSessionProbe,
                mediaCommandSender,
                mediaApplicationLauncher,
                systemVolumeService,
                startupRegistrationService,
                mediaTrackInfoSource,
                new WindowsUserIdleTimeSource(),
                systemResumeSource,
                protectedGameMonitor,
                foregroundApplicationProbe,
                messageNotificationSource,
                new WindowsRecycleBinService(),
                new WindowsWindowWorkAreaProvider(),
                initialVisualState,
                previewExit,
                previewMusicTransition,
                previewBodyHitDebug,
                previewDragCycle,
                previewMediaControls,
                previewTrackInfo,
                liveTrackInfoQa,
                previewSettings,
                previewTray,
                previewSystemResume,
                previewLongIdle,
                previewGenshinLaunch,
                previewGenshinCameo,
                previewMessageNotification,
                previewEdgeDock,
                previewBottomControlsLayout,
                previewBodyReaction,
                showQaTaskbar,
                persistSettings: !isPreviewOrQaRun);
            MainWindow = window;
            window.Show();
            _logger.Info("app.started", "Runtime animation window started.");
        }
        catch (Exception exception)
        {
            _logger.Error("app.start_failed", exception);
            MessageBox.Show(
                "洛天依桌宠启动失败，诊断信息已保存到本地日志。",
                "洛天依桌宠",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static MessageProvider? ParseMessageNotificationPreview(IEnumerable<string> arguments)
    {
        string? value = arguments
            .FirstOrDefault(argument => argument.StartsWith(
                "--qa-message-notification=",
                StringComparison.OrdinalIgnoreCase))?
            .Split('=', 2)[1];
        return value?.ToLowerInvariant() switch
        {
            "qq" => MessageProvider.Qq,
            "wechat" or "weixin" => MessageProvider.WeChat,
            _ => null,
        };
    }

    private static EdgeDockSide? ParseEdgeDockPreview(IEnumerable<string> arguments)
    {
        string? value = arguments
            .FirstOrDefault(argument => argument.StartsWith(
                "--qa-edge-dock=",
                StringComparison.OrdinalIgnoreCase))?
            .Split('=', 2)[1];
        return value?.ToLowerInvariant() switch
        {
            "left" => EdgeDockSide.Left,
            "right" => EdgeDockSide.Right,
            "bottom" => EdgeDockSide.Bottom,
            _ => null,
        };
    }

    private static string? TryGetPackageFamilyName()
    {
        try
        {
            return global::Windows.ApplicationModel.Package.Current.Id.FamilyName;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Info("app.exited", $"Exit code: {e.ApplicationExitCode}.");
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("app.dispatcher_unhandled", e.Exception);
        e.Handled = true;
        MessageBox.Show(
            "桌宠遇到未处理错误并将安全退出。",
            "洛天依桌宠",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(-1);
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger?.Error("app.domain_unhandled", exception);
        }
    }

    private static AnimationCatalog? LoadAnimationCatalog(IAppLogger logger)
    {
        string assetsRoot = Path.Combine(AppContext.BaseDirectory, "assets");
        string catalogPath = Path.Combine(assetsRoot, "manifests", "animations.json");
        try
        {
            AnimationCatalog catalog = AnimationCatalog.Load(assetsRoot, catalogPath);
            logger.Info("animation.catalog_loaded", $"Loaded {catalog.Assets.Count} animations.");
            return catalog;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            logger.Error("animation.catalog_failed", exception);
            return null;
        }
    }

    private static ISystemVolumeService? CreateSystemVolumeService(
        AppSettings settings,
        IAppLogger logger)
    {
        try
        {
            return new WindowsSystemVolumeService(
                new CoreAudioSystemVolumeBackend(),
                settings.Volume,
                settings.Safety);
        }
        catch (Exception exception) when (
            exception is System.Runtime.InteropServices.COMException or
            InvalidOperationException or
            UnauthorizedAccessException)
        {
            logger.Error("volume.endpoint_initialization_failed", exception);
            return null;
        }
    }

    private static PetVisualState GetInitialVisualState(IReadOnlyCollection<string> arguments)
    {
        bool useFullBodyMode = arguments.Contains("--preview-full-body", StringComparer.OrdinalIgnoreCase) ||
            arguments.Contains("--preview-body-hit-debug", StringComparer.OrdinalIgnoreCase) ||
            arguments.Any(argument => argument.StartsWith(
                "--preview-body-reaction=",
                StringComparison.OrdinalIgnoreCase));
        PetDisplayMode displayMode = useFullBodyMode
            ? PetDisplayMode.FullBodyInteractive
            : PetDisplayMode.Compact;
        PetContinuousState continuousState = arguments.Contains(
            "--preview-music",
            StringComparer.OrdinalIgnoreCase)
                ? PetContinuousState.MusicPlaying
                : PetContinuousState.Idle;

        return new PetVisualState(displayMode, continuousState);
    }
}
