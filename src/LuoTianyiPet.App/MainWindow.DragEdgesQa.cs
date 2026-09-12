using System.IO;
using System.Windows;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunDragEdgesQaAsync()
    {
        if (_persistSettings || _animationCatalog is null) return;
        string directory = Path.Combine(AppContext.BaseDirectory, "DragEdgesQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool condition, string label) => checks.Add((condition ? "PASS " : "FAIL ") + label);
        try
        {
            await Task.Delay(600);
            _idleSceneTimer.Stop();
            _timeSceneTimer.Stop();
            _trackInfoRefreshTimer.Stop();
            CancelTimeGreetingPresentation(false, "Isolated drag geometry QA.");
            _stateMachine.CancelActiveReaction();
            _stateMachine.SetContinuousState(PetContinuousState.Idle);
            SetPositionLocked(false);
            SetMusicIslandsVisible(true);
            TrackInfoBubble.Effect = null;
            MediaControls.Effect = null;
            foreach (string appearance in new[] { AppearanceOptionIds.FullBodyClassicCatEars,
                AppearanceOptionIds.FullBodyCrystalDress, AppearanceOptionIds.FullBodyLongHair, "music" })
            {
                ApplyAppearancePreferences(_settings.Appearance with
                {
                    FullBodyStyle = appearance == "music" ? AppearanceOptionIds.FullBodyClassicCatEars : appearance,
                });
                await Task.Delay(500);
                _stateMachine.SetContinuousState(appearance == "music" ? PetContinuousState.MusicPlaying : PetContinuousState.Idle);
                foreach (int scale in new[] { 50, 100, 150, 200 })
                foreach (string edge in new[] { "right", "left", "top", "bottom", "top-right", "bottom-left" })
                {
                    string label = $"{appearance} {scale}% {edge}";
                    SetDisplayScalePercent(scale, false);
                    PlayResolvedContinuousAnimation();
                    DesktopRectangle area = GetCurrentWorkArea();
                    ApplyAccessoryLayout(AccessoryLayout.Split, false);
                    Left = area.Left + (area.Width - Width) / 2;
                    Top = area.Top + (area.Height - Height) / 2;
                    _dragPressScreenPoint = new Point(600, 500);
                    BeginWindowDrag();
                    if (_classicDragExpansionStarted)
                    {
                        var expansion = _animationCatalog.GetRequired(PetVisualState.CompactDraggingAnimation);
                        ShowAnimationFrame(expansion.Id, expansion.FrameDurationsMilliseconds.Count - 1);
                    }
                    DesktopRectangle before = GetPetImageDesktopBounds();
                    double dx = edge.Contains("right") ? area.Right - before.Right :
                        edge.Contains("left") ? area.Left - before.Left : 0;
                    double dy = edge.StartsWith("top") ? area.Top - before.Top :
                        edge.StartsWith("bottom") ? area.Bottom - before.Bottom : 0;
                    MoveWindowWithPointer(new Point(600 + dx, 500 + dy), DateTimeOffset.Now);
                    DesktopRectangle contact = GetPetImageDesktopBounds();
                    Check(_dragEdgeCandidate == EdgeDockSide.None, $"{label}: contact does not trigger hiding");
                    double contactGap = edge.Contains("right") ? area.Right - contact.Right :
                        edge.Contains("left") ? contact.Left - area.Left :
                        edge == "top" ? contact.Top - area.Top : area.Bottom - contact.Bottom;
                    Check(Math.Abs(contactGap) < 1, $"{label}: pointer reaches visible edge");
                    if (appearance == AppearanceOptionIds.FullBodyClassicCatEars && scale == 200 && edge == "right")
                        CaptureQuickActionsQa(this, Path.Combine(directory, "right-before.png"));
                    EndWindowDrag();
                    await Task.Delay(550);
                    DesktopRectangle after = GetPetImageDesktopBounds();
                    double gap = edge.Contains("right") ? area.Right - after.Right :
                        edge.Contains("left") ? after.Left - area.Left :
                        edge == "top" ? after.Top - area.Top : area.Bottom - after.Bottom;
                    Check(Math.Abs(gap) < 1, $"{label}: restored artwork touches edge; gap={gap:F2}");
                    if (edge.StartsWith("top"))
                        Check(Math.Abs(after.Top - area.Top) < 1, $"{label}: top contact also preserved");
                    if (edge.StartsWith("bottom"))
                        Check(Math.Abs(after.Bottom - area.Bottom) < 1, $"{label}: bottom contact also preserved");
                    Check(_animationPlayer?.CurrentAnimationId == _stateMachine.Resolve(DateTimeOffset.Now).AnimationId &&
                        _edgeDockSide == EdgeDockSide.None, $"{label}: resolved state remains visible");
                    UpdateAccessoryLayoutForCurrentPosition();
                    Check(Math.Abs(GetPetImageDesktopBounds().Left - after.Left) < 1 &&
                        Math.Abs(GetPetImageDesktopBounds().Top - after.Top) < 1,
                        $"{label}: accessory refresh preserves position");
                    if (appearance == AppearanceOptionIds.FullBodyClassicCatEars && scale == 200 && edge == "right")
                    {
                        ShowTrackInfo(new MediaTrackSnapshot(true, true, "贴边回归测试", "测试歌手"), true);
                        _trackInfoMotion.Show(animate: false);
                        _mediaControlsMotion.Show(animate: false);
                        CaptureQuickActionsQa(this, Path.Combine(directory, "right-after.png"));
                    }
                }
            }

            // Drive actual overscan into the existing preview/docking pipeline.
            _stateMachine.SetContinuousState(PetContinuousState.Idle);
            SetDisplayScalePercent(100, false);
            foreach (EdgeDockSide side in new[] { EdgeDockSide.Left, EdgeDockSide.Right, EdgeDockSide.Bottom })
            {
                PlayResolvedContinuousAnimation();
                DesktopRectangle area = GetCurrentWorkArea();
                Left = area.Left + (area.Width - Width) / 2;
                Top = area.Top + (area.Height - Height) / 2;
                _dragPressScreenPoint = new Point(600, 500);
                BeginWindowDrag();
                DesktopRectangle intent = GetDragIntentPetDesktopBounds();
                double dx = side == EdgeDockSide.Right ? area.Right - intent.Right + intent.Width * .45 :
                    side == EdgeDockSide.Left ? area.Left - intent.Left - intent.Width * .45 : 0;
                double dy = side == EdgeDockSide.Bottom ? area.Bottom - intent.Bottom + intent.Height * .45 : 0;
                MoveWindowWithPointer(new Point(600 + dx, 500 + dy), DateTimeOffset.Now);
                Check(_dragEdgeCandidate == side, $"Actual overscan selects {side} hiding preview");
                EndWindowDrag();
                await Task.Delay(1200);
                Check(_edgeDockSide == side, $"Actual overscan still docks on {side}");
                ShowPetFromTray();
                Check(_edgeDockSide == EdgeDockSide.None && IsVisible, $"Recall from {side} remains available");
            }
            File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
            Application.Current.Shutdown(checks.Any(check => check.StartsWith("FAIL")) ? 1 : 0);
        }
        catch (Exception exception)
        {
            checks.Add("FAIL " + exception);
            File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
            Application.Current.Shutdown(1);
        }
    }
}
