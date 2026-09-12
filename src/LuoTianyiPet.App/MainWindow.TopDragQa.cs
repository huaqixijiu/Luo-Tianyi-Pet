using System.IO;
using System.Windows;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    // Drives real WPF drag/restore/layout code without sending input or saving user settings.
    private async Task RunTopDragQaAsync()
    {
        if (_persistSettings) return;
        string directory = Path.Combine(AppContext.BaseDirectory, "TopDragQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool condition, string name)
        {
            checks.Add((condition ? "PASS " : "FAIL ") + name);
        }
        try
        {
            await Task.Delay(700);
            // Synthetic pointer calls intentionally do not reset Windows' global idle clock.
            _idleSceneTimer.Stop();
            _timeSceneTimer.Stop();
            _stateMachine.SetContinuousState(PetContinuousState.Idle);
            SetPositionLocked(false);
            foreach (int scale in new[] { 50, 100, 150, 200 })
            foreach (bool islands in new[] { false, true })
            {
                SetMusicIslandsVisible(islands);
                SetDisplayScalePercent(scale, false);
                _stateMachine.CancelActiveReaction();
                PlayResolvedContinuousAnimation();
                DesktopRectangle work = GetCurrentWorkArea();
                Left = work.Left + (work.Width - ActualWidth) / 2;
                Top = work.Top + 160;
                UpdateAccessoryLayoutForCurrentPosition();
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    _dragPressScreenPoint = new Point(600, 500);
                    BeginWindowDrag();
                    await Task.Delay(100);
                    MoveWindowWithPointer(new Point(600, -1500), DateTimeOffset.Now);
                    await Task.Delay(100);
                    DesktopRectangle before = GetPetImageDesktopBounds();
                    Check(Math.Abs(before.Top - work.Top) < 1,
                        $"{scale}% islands={islands} repeat={repeat}: dragging reaches top ({before.Top:F2})");
                    if (scale == 200 && islands && repeat == 0)
                        CaptureQuickActionsQa(this, Path.Combine(directory, "dragging.png"));
                    EndWindowDrag();
                    await Task.Delay(650);
                    DesktopRectangle after = GetPetImageDesktopBounds();
                    Check(_animationPlayer?.CurrentAnimationId == AppearanceOptionIds.ClassicCatEarsAnimation,
                        $"{scale}% islands={islands} repeat={repeat}: idle restored");
                    Check(Math.Abs(after.Top - work.Top) < 1,
                        $"{scale}% islands={islands} repeat={repeat}: release stays at top ({after.Top:F2})");
                    Check(_accessoryLayout == AccessoryLayout.BelowPet,
                        $"{scale}% islands={islands} repeat={repeat}: islands stay below pet");
                    UpdateAccessoryLayoutForCurrentPosition();
                    Check(Math.Abs(GetPetImageDesktopBounds().Top - after.Top) < 1,
                        $"{scale}% islands={islands} repeat={repeat}: layout refresh has no drift");
                    if (scale == 200 && islands && repeat == 0)
                        CaptureQuickActionsQa(this, Path.Combine(directory, "released.png"));
                }
            }
            SetDisplayScalePercent(100, false);
            DesktopRectangle area = GetCurrentWorkArea();
            Top = area.Top + 220;
            UpdateAccessoryLayoutForCurrentPosition();
            double idleBottom = GetPetImageDesktopBounds().Bottom;
            _dragPressScreenPoint = new Point(600, 500);
            BeginWindowDrag();
            MoveWindowWithPointer(new Point(600, 520), DateTimeOffset.Now);
            EndWindowDrag();
            await Task.Delay(650);
            Check(Math.Abs(GetPetImageDesktopBounds().Bottom - idleBottom - 20) < 1,
                "Ordinary center drag preserves idle bottom and requested movement");

            BeginWindowDrag();
            MoveWindowWithPointer(new Point(600, -1500), DateTimeOffset.Now);
            EndWindowDrag();
            await Task.Delay(35);
            BeginWindowDrag();
            MoveWindowWithPointer(new Point(600, 750), DateTimeOffset.Now);
            double interruptedTop = Top;
            await Task.Delay(400);
            Check(_isWindowDragging && Math.Abs(Top - interruptedTop) < 1 &&
                _animationPlayer?.CurrentAnimationId == PetVisualState.CompactDraggingAnimation,
                "New drag cancels pending top restoration without stale movement or idle swap");
            EndWindowDrag();
            await Task.Delay(650);

            BeginWindowDrag();
            MoveWindowWithPointer(new Point(600, -1500), DateTimeOffset.Now);
            _stateMachine.SetContinuousState(PetContinuousState.MusicPlaying);
            EndWindowDrag();
            await Task.Delay(650);
            Check(_animationPlayer?.CurrentAnimationId == _stateMachine.Resolve(DateTimeOffset.Now).AnimationId &&
                _stateMachine.VisualState.ContinuousState == PetContinuousState.MusicPlaying &&
                Math.Abs(GetPetImageDesktopBounds().Top - area.Top) < 1,
                "Music beginning during expansion restores current music at the top");
            _stateMachine.SetContinuousState(PetContinuousState.Idle);

            foreach (string style in new[] { AppearanceOptionIds.FullBodyLongHair, AppearanceOptionIds.FullBodyCrystalDress })
            {
                ApplyAppearancePreferences(_settings.Appearance with { FullBodyStyle = style });
                await Task.Delay(650);
                _dragPressScreenPoint = new Point(600, 500);
                BeginWindowDrag();
                MoveWindowWithPointer(new Point(600, -1500), DateTimeOffset.Now);
                double beforeTop = GetPetImageDesktopBounds().Top;
                EndWindowDrag();
                await Task.Delay(650);
                Check(Math.Abs(GetPetImageDesktopBounds().Top - beforeTop) < 1 && !_classicDragExpansionStarted,
                    $"{style}: top drag retains existing in-place artwork behavior");
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
