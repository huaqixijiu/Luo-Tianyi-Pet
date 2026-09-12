using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunFishingQaAsync()
    {
        if (_persistSettings || _animationCatalog is null) return;
        string directory = Path.Combine(AppContext.BaseDirectory, "FishingQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool condition, string label) => checks.Add((condition ? "PASS " : "FAIL ") + label);
        async Task StartFishing()
        {
            _stateMachine.CancelActiveReaction();
            _stateMachine.SetContinuousState(PetContinuousState.Idle);
            ApplyIdleScene(TimeSpan.FromMinutes(2));
            await Task.Delay(600);
            Check(_animationPlayer?.CurrentAnimationId == PetVisualState.MediumIdleCountdownAnimation,
                "Fishing artwork starts at two minutes");
        }
        try
        {
            await Task.Delay(600);
            _idleSceneTimer.Stop();
            _timeSceneTimer.Stop();
            CancelTimeGreetingPresentation(false, "Isolated fishing QA.");
            ApplyAppearancePreferences(_settings.Appearance with
            {
                FullBodyStyle = AppearanceOptionIds.FullBodyClassicCatEars,
            });
            SetPositionLocked(false);
            SetDisplayScalePercent(150, false);
            await Task.Delay(600);
            await StartFishing();
            long? start = _fishingCountdownStartedTimestamp;
            for (int i = 0; i < 5; i++)
            {
                OnMouseMove(this, new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount));
                ApplyIdleScene(TimeSpan.Zero);
                await Task.Delay(250);
            }
            Check(_stateMachine.VisualState.ContinuousState == PetContinuousState.MediumIdleCountdown,
                "Mouse movement and Windows idle reset do not cancel fishing");
            Check(start.HasValue && start == _fishingCountdownStartedTimestamp,
                "Movement does not restart the countdown clock");
            CaptureQuickActionsQa(this, Path.Combine(directory, "white-background.png"));

            _fishingCountdownStartedTimestamp = Stopwatch.GetTimestamp() - 60 * Stopwatch.Frequency;
            ApplyIdleScene(TimeSpan.Zero);
            await Task.Delay(600);
            Check(_stateMachine.VisualState.ContinuousState == PetContinuousState.MediumIdle,
                "Sixty seconds completes into Hehe despite mouse movement");
            ApplyIdleScene(TimeSpan.Zero);
            Check(_stateMachine.VisualState.ContinuousState == PetContinuousState.MediumIdle,
                "Hehe is not immediately cancelled by the next input poll");

            await StartFishing();
            OnMouseLeftButtonDown(this, new MouseButtonEventArgs(Mouse.PrimaryDevice,
                Environment.TickCount, MouseButton.Left) { RoutedEvent = MouseLeftButtonDownEvent });
            Mouse.Capture(null);
            _pointerGesture.Cancel();
            _singleClickTimer.Stop();
            Check(_stateMachine.VisualState.ContinuousState == PetContinuousState.Idle,
                "Actual pet left-button handler exits fishing");
            ApplyIdleScene(TimeSpan.Zero);
            Check(_stateMachine.VisualState.ContinuousState == PetContinuousState.Idle,
                "Next poll does not restore a cancelled countdown");

            await StartFishing();
            _dragPressScreenPoint = new Point(600, 500);
            BeginWindowDrag();
            Check(_isWindowDragging, "Direct drag enters dragging");
            EndWindowDrag();
            await Task.Delay(600);
            Check(_stateMachine.VisualState.ContinuousState == PetContinuousState.Idle,
                "Drag release restores idle instead of fishing");
            await StartFishing();
            Check(FishingCountdownElapsed < TimeSpan.FromSeconds(2),
                "New countdown receives a fresh monotonic start time");
            _stateMachine.SetContinuousState(PetContinuousState.MusicPlaying);
            ApplyIdleScene(TimeSpan.Zero);
            Check(_stateMachine.VisualState.ContinuousState == PetContinuousState.MusicPlaying,
                "Music retains priority");
        }
        catch (Exception exception)
        {
            checks.Add("FAIL " + exception);
        }
        File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
        Application.Current.Shutdown(checks.Any(check => check.StartsWith("FAIL")) ? 1 : 0);
    }
}
