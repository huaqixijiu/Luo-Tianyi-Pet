using System.IO;
using System.Windows;
using System.Windows.Input;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow : Window
{
    private AppSettings _settings;
    private readonly ISettingsStore _settingsStore;
    private readonly IAppLogger _logger;

    public MainWindow(
        AppSettings settings,
        ISettingsStore settingsStore,
        IAppLogger logger)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _logger = logger;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Topmost = _settings.Window.AlwaysOnTop;
        TopmostMenuItem.IsChecked = Topmost;

        Rect workArea = SystemParameters.WorkArea;
        double desiredLeft = _settings.Window.Left ?? workArea.Right - ActualWidth - 32;
        double desiredTop = _settings.Window.Top ?? workArea.Bottom - ActualHeight - 32;

        Left = Math.Clamp(desiredLeft, workArea.Left, Math.Max(workArea.Left, workArea.Right - ActualWidth));
        Top = Math.Clamp(desiredTop, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - ActualHeight));
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
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

    private void OnToggleTopmost(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostMenuItem.IsChecked;
        _logger.Info("window.topmost_changed", Topmost ? "Enabled." : "Disabled.");
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
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
}
