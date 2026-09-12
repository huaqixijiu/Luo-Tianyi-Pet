using System.Diagnostics;
using System.IO;
using System.Windows;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunAfternoonGreetingQaAsync()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "AfternoonGreetingQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks.Add("PASS " + label);
        }
        try
        {
            await Task.Delay(600);
            _idleSceneTimer.Stop();
            _timeSceneTimer.Stop();
            CancelTimeGreetingPresentation(false, "Afternoon QA");
            _stateMachine.CancelActiveReaction();
            _stateMachine.SetContinuousState(PetContinuousState.Idle);
            SetDisplayScalePercent(100, false);
            StartupTimeSceneDecision afternoon = StartupTimeSceneResolver.Resolve(new TimeOnly(13, 30));
            Check(afternoon.AnimationId == "startup-afternoon-hurry", "13:30 selects the downloaded hurry animation");
            var asset = _animationCatalog!.GetRequired(afternoon.AnimationId);
            Check(asset.FrameDurationsMilliseconds.Count == 9 &&
                asset.FrameDurationsMilliseconds.All(value => value == 100) && asset.LoopCount == 0,
                "Nine original frames loop at native 100 ms timing");
            Stopwatch elapsed = Stopwatch.StartNew();
            Task<bool> presentation = PlayTimeGreetingPresentationAsync(afternoon, "qa.afternoon");
            await Task.Delay(1000);
            Check(_animationPlayer?.CurrentAnimationId == afternoon.AnimationId, "Real greeting path displays the new artwork");
            HashSet<int> frames = [];
            for (int i = 0; i < 12; i++)
            {
                frames.Add(_animationPlayer!.CurrentFrameIndex);
                await Task.Delay(100);
            }
            Check(frames.Count > 2, "WPF actually advances multiple animation frames");
            Check(PetShakeTransform.Y == 0 && PetShakeTransform.X == 0,
                "Original GIF has no extra procedural floating");
            CaptureQuickActionsQa(this, Path.Combine(directory, "hurry.png"));
            Check(!presentation.IsCompleted, "Greeting survives multiple 900 ms animation loops");
            Check(await presentation, "Ten-second greeting completes normally");
            Check(elapsed.Elapsed >= TimeSpan.FromSeconds(10), "Presentation retains the ten-second duration");
            Check(_timeGreetingReactionToken is null && _animationPlayer?.CurrentAnimationId != afternoon.AnimationId,
                "Normal completion restores the persistent appearance");
            Task<bool> cancelled = PlayTimeGreetingPresentationAsync(afternoon, "qa.afternoon.cancel");
            await Task.Delay(900);
            CancelTimeGreetingPresentation(true, "QA input cancellation");
            Check(!await cancelled && _timeGreetingReactionToken is null, "Input cancellation releases the greeting");
            _stateMachine.SetContinuousState(PetContinuousState.MusicPlaying);
            Check(!await PlayTimeGreetingPresentationAsync(afternoon, "qa.afternoon.music"),
                "Playing music suppresses the afternoon greeting");
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
}
