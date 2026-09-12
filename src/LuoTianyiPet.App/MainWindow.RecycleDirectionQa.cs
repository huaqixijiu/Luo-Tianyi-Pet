using System.IO;
using System.Windows;
using System.Windows.Media;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunRecycleDirectionQaAsync()
    {
        if (_persistSettings || _animationCatalog is null) return;
        string directory = Path.Combine(AppContext.BaseDirectory, "RecycleDirectionQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool condition, string label) => checks.Add((condition ? "PASS " : "FAIL ") + label);
        bool Upright() => PetDirectionTransform.ScaleX == 1 && PetImage.RenderTransform.Value.M11 == 1 &&
            !_bodyReactionMirrorActive;
        void SeedMirror(string kind)
        {
            ApplyBodyReactionMirror(kind == "body");
            if (kind is "chase" or "both") PetDirectionTransform.ScaleX = -1;
            SetEdgeMirror(kind is "edge" or "both");
        }
        try
        {
            await Task.Delay(600);
            _idleSceneTimer.Stop();
            _timeSceneTimer.Stop();
            _trackInfoRefreshTimer.Stop();
            CancelTimeGreetingPresentation(false, "Isolated recycle direction QA.");
            CancelMessageNotificationPresentation(false);
            ApplyAppearancePreferences(_settings.Appearance with { FullBodyStyle = AppearanceOptionIds.FullBodyClassicCatEars });
            SetMusicIslandsVisible(false);
            Root.Background = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString("#0756B8")!;
            await Task.Delay(400);
            foreach (int scale in new[] { 100, 150, 200 })
            foreach (string kind in new[] { "body", "chase", "edge", "both" })
            {
                _stateMachine.CancelActiveReaction();
                _stateMachine.SetContinuousState(PetContinuousState.Idle);
                SetDisplayScalePercent(scale, false);
                PlayResolvedContinuousAnimation();
                SeedMirror(kind);
                StartFileDragPresentation();
                await Task.Delay(80);
                Check(_fileDragPresentationActive && _animationPlayer?.CurrentAnimationId == FileDropPromptAnimation && Upright(),
                    $"{scale}% {kind}: Give-me starts upright in both transform layers");
                Guid? token = _fileDropReactionToken;
                StartFileDragPresentation();
                Check(token == _fileDropReactionToken, $"{scale}% {kind}: repeated DragOver does not restart prompt");
                await Task.Delay(1100);
                Check(_animationPlayer?.CurrentAnimationId == FileDropPromptAnimation && Upright(),
                    $"{scale}% {kind}: prompt holds upright after playback");
                if (kind == "both") CaptureQuickActionsQa(this, Path.Combine(directory, $"give-me-{scale}.png"));
                FinishFileDragPresentation(false);
                Check(!_fileDragPresentationActive && !_fileDragCursorOverrideActive,
                    $"{scale}% {kind}: leaving releases prompt and cursor");
                SeedMirror(kind);
                await PlayReactionAsync(FileDropSuccessAnimation, ReactionPriority.UserInteraction);
                Check(_animationPlayer?.CurrentAnimationId == FileDropSuccessAnimation && Upright(),
                    $"{scale}% {kind}: recycle success is also upright");
                if (kind == "both") CaptureQuickActionsQa(this, Path.Combine(directory, $"success-{scale}.png"));
            }
            _stateMachine.CancelActiveReaction();
            PlayResolvedContinuousAnimation();
            ApplyBodyReactionMirror(true);
            PlayAnimation("resonance-please");
            Check(PetDirectionTransform.ScaleX == -1, "Other intentional body mirrors remain available");
            SetEdgeMirror(true);
            PlayAnimation("twelfth-anniversary-peek");
            Check(PetImage.RenderTransform.Value.M11 == -1, "Edge docking retains its own mirror");
            _systemSessionUnavailable = true;
            Check(!IsFileDropEnvironmentSafe(), "Locked/unavailable session still rejects file drop");
            _systemSessionUnavailable = false;
            _stateMachine.CancelActiveReaction();
            _stateMachine.TryStartReaction(new ReactionRequest("resonance-please", ReactionPriority.Exit,
                DateTimeOffset.Now.AddSeconds(20), InterruptibleByDrag: false), DateTimeOffset.Now);
            ApplyBodyReactionMirror(true);
            StartFileDragPresentation();
            Check(!_fileDragPresentationActive && PetDirectionTransform.ScaleX == -1,
                "Rejected prompt does not change higher-priority artwork direction");
            FinishFileDragPresentation(false);
        }
        catch (Exception exception) { checks.Add("FAIL " + exception); }
        File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
        Application.Current.Shutdown(checks.Any(check => check.StartsWith("FAIL")) ? 1 : 0);
    }
}
