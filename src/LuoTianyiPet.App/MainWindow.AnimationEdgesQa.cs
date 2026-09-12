using System.IO;
using System.Windows;
using System.Windows.Media;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunAnimationEdgesQaAsync()
    {
        if (_persistSettings || _animationCatalog is null) return;
        string directory = Path.Combine(AppContext.BaseDirectory, "AnimationEdgesQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        try
        {
            await Task.Delay(600);
            _idleSceneTimer.Stop();
            _timeSceneTimer.Stop();
            _trackInfoRefreshTimer.Stop();
            CancelTimeGreetingPresentation(false, "Isolated animation edge QA.");
            _stateMachine.CancelActiveReaction();
            SetMusicIslandsVisible(false);
            foreach (int scale in new[] { 100, 150, 200 })
            {
                SetDisplayScalePercent(scale, save: false);
                foreach (var asset in _animationCatalog.Assets)
                foreach (string color in new[] { "102033", "E8EBEF" })
                {
                    Root.Background = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString("#" + color)!;
                    ShowAnimationFrame(asset.Id, asset.FrameDurationsMilliseconds.Count / 2);
                    UpdateLayout();
                    await Task.Delay(25);
                    bool passed = _animationPlayer?.CurrentAnimationId == asset.Id &&
                        PetImage.Source is not null && PetImage.Visibility == Visibility.Visible &&
                        FallbackSurface.Visibility != Visibility.Visible &&
                        Math.Abs(PetImage.Width - asset.DisplayWidth * scale / 100.0) < .01;
                    checks.Add((passed ? "PASS " : "FAIL ") + $"{asset.Id} {scale}% {color}");
                    CaptureQuickActionsQa(this, Path.Combine(directory, $"{asset.Id}-{scale}-{color}.png"));
                }
                File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
            }
        }
        catch (Exception exception)
        {
            checks.Add("FAIL " + exception);
        }
        File.WriteAllLines(Path.Combine(directory, "result.txt"), checks);
        Application.Current.Shutdown(checks.Any(check => check.StartsWith("FAIL")) ? 1 : 0);
    }
}
