using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private PetQuickPanel? _petQuickPanel;
    private bool _hiddenByUser;

    private bool CanShowMusicIslands => _settings.Media.ShowMusicIslands &&
        !_isClosing && !_hiddenByUser && !_bunChaseActive &&
        _edgeDockSide == EdgeDockSide.None && _petQuickPanel?.IsVisible != true;

    private void OnPetMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isClosing || _isWindowDragging || _fileDropInProgress || _bunChaseActive)
        {
            return;
        }
        PointerPoint? point = NormalizeToPetImage(ToPointerPoint(e.GetPosition(this)));
        if (point is not PointerPoint hit || !IsOpaquePetPixel(hit))
        {
            return;
        }
        e.Handled = true;
        _pointerGesture.Cancel();
        _pettingGesture.Cancel();
        _singleClickTimer.Stop();
        if (_petQuickPanel?.IsVisible == true)
        {
            _petQuickPanel.Hide();
            return;
        }
        HideMusicIslands();
        _petQuickPanel ??= new PetQuickPanel(
            () => _settings,
            SetPositionLocked,
            enabled => SetPermanentTopmost(enabled, save: true),
            SetMusicIslandsVisible,
            percent => SetDisplayScalePercent(percent, save: true));
        DesktopRectangle bounds = GetPetImageAlphaBoundsInWindow();
        _petQuickPanel.ShowNearPet(
            new DesktopRectangle(Left + bounds.Left, Top + bounds.Top, bounds.Width, bounds.Height),
            GetQuickActionsWorkArea());
    }

    private void SetPositionLocked(bool locked)
    {
        if (locked && _isWindowDragging)
        {
            EndWindowDrag();
            _pointerGesture.Cancel();
            ReleaseMouseCapture();
        }
        _settings = _settings with { Window = _settings.Window with { LockPosition = locked } };
        if (_persistSettings)
        {
            _ = SaveSettingsAsync("settings.position_lock_saved", "Position lock preference saved.");
        }
    }

    private void SetMusicIslandsVisible(bool visible)
    {
        _settings = _settings with { Media = _settings.Media with { ShowMusicIslands = visible } };
        if (!visible)
        {
            HideMusicIslands();
        }
        else if (CanShowMusicIslands)
        {
            if (_settings.Media.EnableCloudMusicShortcutControl)
            {
                _mediaControlsMotion.Show();
                _mediaControlsHideTimer.Start();
            }
            if (_lastTrackSnapshot.HasTrack)
            {
                ShowTrackInfo(_lastTrackSnapshot, holdAfterLeave: true);
            }
            else
            {
                ShowTrackInfoUnavailable();
                _trackInfoHideTimer.Start();
            }
            _ = RefreshTrackInfoAsync(showWhenFound: true);
        }
        if (_persistSettings)
        {
            _ = SaveSettingsAsync("settings.music_islands_saved", "Music island preference saved.");
        }
    }

    private void HideMusicIslands()
    {
        _mediaControlsHideTimer.Stop();
        _trackInfoHideTimer.Stop();
        _trackInfoShowRequested = false;
        CloudMusicVolumePopup.IsOpen = false;
        _mediaControlsMotion.Hide(animate: false);
        _trackInfoMotion.Hide(animate: false);
    }

    private void ShowPetFromTray()
    {
        if (_isClosing) return;
        _petQuickPanel?.Hide();
        _hiddenByUser = false;
        if (_edgeDockSide != EdgeDockSide.None)
        {
            ++_edgeDockAnimationGeneration;
            _edgeDockSide = EdgeDockSide.None;
            _edgeDockRevealed = false;
            _dragEdgeCandidate = EdgeDockSide.None;
            SetEdgeMirror(false);
            EdgeDockHandle.Visibility = Visibility.Collapsed;
            PetImage.Clip = null;
            PetImage.IsHitTestVisible = true;
            PlayResolvedContinuousAnimation();
        }
        Show();
        WindowState = WindowState.Normal;
        DesktopRectangle work = GetQuickActionsWorkArea();
        DesktopRectangle pet = GetStableStageBoundsInWindow();
        Left = Clamp(Left, work.Left - pet.Left, work.Right - pet.Right);
        Top = Clamp(Top, work.Top - pet.Top, work.Bottom - pet.Bottom);
        UpdateAccessoryLayoutForCurrentPosition();
        _ = WindowsWindowZOrder.SetTopmostWithoutActivation(new WindowInteropHelper(this).Handle, true);
        ApplyEffectiveTopmost();
        _trayIcon?.RefreshChecks();
    }

    private void HidePetFromTray()
    {
        if (_isClosing) return;
        _hiddenByUser = true;
        _petQuickPanel?.Hide();
        CancelGenshinPresentations(restoreContinuousAnimation: true);
        CancelMessageNotificationPresentation(restoreContinuousAnimation: true);
        CancelBunChase(restorePosition: true, restoreContinuousAnimation: true);
        HideMusicIslands();
        Hide();
        _trayIcon?.RefreshChecks();
    }

    private DesktopRectangle GetQuickActionsWorkArea()
    {
        try
        {
            DesktopRectangle pixels = _windowWorkAreaProvider.GetForWindow(new WindowInteropHelper(this).Handle);
            Point start = ConvertScreenPixelsToDips(new PointerPoint(pixels.Left, pixels.Top));
            Point end = ConvertScreenPixelsToDips(new PointerPoint(pixels.Right, pixels.Bottom));
            return new DesktopRectangle(start.X, start.Y, end.X - start.X, end.Y - start.Y);
        }
        catch (InvalidOperationException)
        {
            Rect fallback = SystemParameters.WorkArea;
            return new DesktopRectangle(fallback.Left, fallback.Top, fallback.Width, fallback.Height);
        }
    }
}
