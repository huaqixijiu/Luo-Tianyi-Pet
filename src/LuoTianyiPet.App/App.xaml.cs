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

        LocalAppPaths paths = new();
        _logger = new FileAppLogger(paths);
        JsonSettingsStore settingsStore = new(paths);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
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
            bool previewMediaControls = e.Args.Contains(
                "--qa-media-controls",
                StringComparer.OrdinalIgnoreCase) || e.Args.Contains(
                    "--qa-shortcut-menu",
                    StringComparer.OrdinalIgnoreCase);
            bool previewTrackInfo = e.Args.Contains(
                "--qa-track-info",
                StringComparer.OrdinalIgnoreCase);
            bool liveTrackInfoQa = e.Args.Contains(
                "--qa-track-info-live",
                StringComparer.OrdinalIgnoreCase);
            bool liveSystemVolumeQa = e.Args.Contains(
                "--qa-system-volume",
                StringComparer.OrdinalIgnoreCase);
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
            IMediaCommandSender mediaCommandSender = new WindowsMediaCommandSender(
                new Win32ShortcutInputBackend(),
                settings.Media,
                settings.Safety);
            ISystemVolumeService? systemVolumeService = !isPreviewOrQaRun || liveSystemVolumeQa
                ? CreateSystemVolumeService(settings, _logger)
                : null;
            IMediaTrackInfoSource? mediaTrackInfoSource = !isPreviewOrQaRun || liveTrackInfoQa
                ? new SystemMediaTrackInfoSource()
                : null;
            MainWindow window = new(
                settings,
                settingsStore,
                _logger,
                animationCatalog,
                audioSessionProbe,
                mediaCommandSender,
                systemVolumeService,
                mediaTrackInfoSource,
                new WindowsUserIdleTimeSource(),
                initialVisualState,
                previewExit,
                previewMusicTransition,
                previewBodyHitDebug,
                previewDragCycle,
                previewMediaControls,
                previewTrackInfo,
                liveTrackInfoQa,
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
