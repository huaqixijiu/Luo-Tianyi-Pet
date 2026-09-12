using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    // Isolated in-process WPF regression: never sends input to other applications.
    private async Task RunQuickActionsQaAsync()
    {
        if (_persistSettings) return;
        string directory = Path.Combine(AppContext.BaseDirectory, "QuickActionsQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add("PASS " + name);
        }
        try
        {
            await Task.Delay(700);
            SetMusicIslandsVisible(false);
            OnRootMouseEnter(this, new MouseEventArgs(Mouse.PrimaryDevice, 0));
            ShowTrackInfo(new MediaTrackSnapshot(true, true, "测试歌曲", "洛天依"), true);
            ShowTrackSwitchPending();
            Check(TrackInfoBubble.Visibility == Visibility.Collapsed &&
                MediaControls.Visibility == Visibility.Collapsed && !MediaControls.IsHitTestVisible,
                "Disabled islands reject hover, track updates and track-switch feedback");
            CaptureQuickActionsQa(this, Path.Combine(directory, "01-hidden.png"));

            SetMusicIslandsVisible(true);
            OnRootMouseEnter(this, new MouseEventArgs(Mouse.PrimaryDevice, 0));
            await Task.Delay(220);
            Check(MediaControls.Visibility == Visibility.Visible && MediaControls.IsHitTestVisible,
                "Enabling islands exposes the complete four-button control surface");
            Check(TrackTitleText.Text == "未在播放", "No track uses a quiet empty state");
            ShowTrackInfo(new MediaTrackSnapshot(true, true, "测试歌曲", "洛天依"), true);
            await Task.Delay(220);
            CaptureQuickActionsQa(this, Path.Combine(directory, "02-visible.png"));
            SetMusicIslandsVisible(false);
            _restoreTrackInfoAfterFeedback = true;
            HideFeedbackBubble(restoreTrackInfo: true);
            // Simulate a metadata response already in flight when the switch was turned off.
            ShowTrackInfo(new MediaTrackSnapshot(true, true, "延迟返回的歌曲", "测试歌手"), true);
            Check(TrackInfoBubble.Visibility == Visibility.Collapsed && !CloudMusicVolumePopup.IsOpen,
                "Late metadata and feedback restoration cannot reopen disabled islands");

            SetPositionLocked(true);
            double left = Left, top = Top;
            BeginWindowDrag();
            Check(!_isWindowDragging && Left == left && Top == top, "Locked position rejects dragging");
            string previousStyle = _settings.Appearance.FullBodyStyle;
            HandlePointerAction(new PointerGestureAction(PointerGestureActionType.ToggleDisplayMode));
            Check(_settings.Appearance.FullBodyStyle != previousStyle, "Locked position still permits double-click appearance changes");
            SetPositionLocked(false);
            BeginWindowDrag();
            Check(_isWindowDragging, "Unlock restores dragging");
            EndWindowDrag();
            await Task.Delay(600);

            AppSettings beforeHide = _settings;
            HidePetFromTray();
            Check(!IsVisible, "Tray hide hides the pet");
            ShowPetFromTray();
            Check(IsVisible && _settings.Window.AlwaysOnTop == beforeHide.Window.AlwaysOnTop,
                "Tray recall restores visibility without changing permanent topmost preference");
            ShowPetFromTray();
            Check(IsVisible, "Repeated tray left action never hides the pet");

            _petQuickPanel = new PetQuickPanel(() => _settings, SetPositionLocked,
                value => SetPermanentTopmost(value, true), SetMusicIslandsVisible,
                value => SetDisplayScalePercent(value, true));
            _petQuickPanel.ShowNearPet(new DesktopRectangle(Left, Top, ActualWidth, ActualHeight), GetQuickActionsWorkArea());
            CaptureQuickActionsQa(_petQuickPanel, Path.Combine(directory, "03-pet-menu.png"));
            _petQuickPanel.Hide();
            TrayQuickPanel tray = new(ShowPetFromTray, HidePetFromTray, () => IsVisible, ShowSettingsDialog, () => { });
            tray.ShowNearTray();
            CaptureQuickActionsQa(tray, Path.Combine(directory, "04-tray-menu.png"));
            tray.Close();
            SettingsWindow settings = new(_settings.Notifications, _settings.Window,
                _settings.FileTreats, _settings.Appearance, _settings.Media, false, null);
            settings.Show();
            await Task.Delay(250);
            CaptureQuickActionsQa(settings, Path.Combine(directory, "05-settings.png"));
            settings.Close();
            File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
            Close();
        }
        catch (Exception exception)
        {
            checks.Add("FAIL " + exception);
            File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
            Application.Current.Shutdown(1);
        }
    }

    private static void CaptureQuickActionsQa(Window window, string path)
    {
        window.UpdateLayout();
        FrameworkElement content = (FrameworkElement)window.Content;
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(window.ActualWidth),
            (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream file = File.Create(path);
        encoder.Save(file);
    }
}
