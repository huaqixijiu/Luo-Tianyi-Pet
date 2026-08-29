using System.Windows;
using System.Windows.Threading;
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

        _singleInstance = SingleInstanceGuard.Acquire(ApplicationId);
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
            MainWindow window = new(settings, settingsStore, _logger);
            MainWindow = window;
            window.Show();
            _logger.Info("app.started", "M0 transparent window started.");
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
}
