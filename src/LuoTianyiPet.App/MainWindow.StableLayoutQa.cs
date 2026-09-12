using System.IO;
using System.Windows;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunStableLayoutQaAsync()
    {
        if (_persistSettings || _animationCatalog is null) return;
        string directory = Path.Combine(AppContext.BaseDirectory, "StableLayoutQa");
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        void Check(bool passed, string label)
        {
            if (!passed) throw new InvalidOperationException(label);
            checks.Add("PASS " + label);
        }
        static bool Near(double a, double b) => Math.Abs(a - b) < 0.1;
        try
        {
            await Task.Delay(600);
            _idleSceneTimer.Stop();
            _timeSceneTimer.Stop();
            _trackInfoRefreshTimer.Stop();
            // Offscreen WPF captures on this machine can omit cached shader surfaces.
            // Validate the real controls without decorative drop shadows in QA only.
            TrackInfoBubble.Effect = null;
            MediaControls.Effect = null;
            SetMusicIslandsVisible(true);
            string? snapshot = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--qa-stable-layout-snapshot="));
            if (snapshot is not null)
            {
                string id = snapshot.Substring(snapshot.IndexOf('=') + 1);
                _ = _animationCatalog.GetRequired(id);
                SetDisplayScalePercent(100, save: false);
                ApplyAccessoryLayout(AccessoryLayout.BelowPet);
                var area = GetCurrentWorkArea();
                Left = area.Right - Width;
                Top = area.Top - PetVisual.Margin.Top;
                PlayAnimation(id);
                ShowTrackInfo(new MediaTrackSnapshot(true, true, "布局回归测试", "测试歌手"), true);
                _trackInfoHideTimer.Stop();
                _mediaControlsHideTimer.Stop();
                _trackInfoMotion.Show(animate: false);
                _mediaControlsMotion.Show(animate: false);
                await Task.Delay(250);
                _trackInfoMotion.Show(animate: false);
                _mediaControlsMotion.Show(animate: false);
                CaptureQuickActionsQa(this, Path.Combine(directory, "snapshot-" + id + ".png"));
                Close();
                return;
            }
            string[] sequence = ["user-chibi-compact-idle", "user-chibi-crystal-full-body-idle",
                "user-chibi-classic-full-body-idle", "ninth-anniversary-music-sway",
                "newyear-one-click-singing", "resonance-enjoy-music", "twelfth-anniversary-call",
                "resonance-expand", "crystal-long-idle-sleep", "crystal-cover-eyes"];
            foreach (int scale in new[] { 50, 100, 150, 200 })
            {
                SetDisplayScalePercent(scale, save: false);
                DesktopRectangle work = GetCurrentWorkArea();
                foreach (string corner in new[] { "center", "top-left", "top-right", "bottom-left", "bottom-right" })
                {
                    ApplyAccessoryLayout(AccessoryLayout.Split);
                    DesktopRectangle stage = GetStableStageBoundsInWindow();
                    Left = corner.EndsWith("left") ? work.Left - stage.Left :
                        corner.EndsWith("right") ? work.Right - stage.Right : work.Left + (work.Width - Width) / 2;
                    Top = corner.StartsWith("top") ? work.Top - stage.Top :
                        corner.StartsWith("bottom") ? work.Bottom - stage.Bottom : work.Top + (work.Height - Height) / 2;
                    UpdateAccessoryLayoutForCurrentPosition();
                    double x = Left, y = Top, width = Width, height = Height;
                    double track = TrackInfoBubble.Width, controls = MediaControlsLayoutScale.ScaleX;
                    AccessoryLayout layout = _accessoryLayout;
                    for (int repeat = 0; repeat < 2; repeat++)
                    foreach (string id in sequence)
                    {
                        var edge = CaptureStageEdgeAlignment();
                        PlayAnimation(id);
                        RestoreStageEdgeAlignment(edge.AlignLeft, edge.AlignRight, edge.AlignBottom, edge.WorkArea);
                        UpdateLayout();
                        UpdateAccessoryLayoutForCurrentPosition();
                        await Task.Delay(20);
                        Check(Near(Left, x) && Near(Top, y) && Near(Width, width) && Near(Height, height) &&
                            Near(track, TrackInfoBubble.Width) && Near(controls, MediaControlsLayoutScale.ScaleX) && layout == _accessoryLayout,
                            $"{scale}% {corner} round {repeat} {id}: stable window, islands and attachment side");
                        var frame = GetPetImageBoundsInWindow();
                        Check(Left + frame.Left >= work.Left - .1 && Left + frame.Right <= work.Right + .1 &&
                            Top + frame.Top >= work.Top - .1 && Top + frame.Bottom <= work.Bottom + .1,
                            $"{scale}% {corner} {id}: artwork remains on screen");
                        if (scale == 100 && corner == "top-right" && repeat == 0 &&
                            (id.Contains("full-body-idle") || id == "ninth-anniversary-music-sway" || id == "newyear-one-click-singing"))
                        {
                            _trackInfoHideTimer.Stop();
                            _mediaControlsHideTimer.Stop();
                            ShowTrackInfo(new MediaTrackSnapshot(true, true, "布局回归测试", "测试歌手"), true);
                            _mediaControlsMotion.Show();
                            _trackInfoHideTimer.Stop();
                            await Task.Delay(200);
                            Check(TrackInfoBubble.Opacity > .99 && MediaControls.Opacity > .99 &&
                                TrackInfoBubble.IsVisible && MediaControls.IsVisible,
                                "Snapshot islands are fully visible: " + id);
                            // Fresh effect instances avoid reusing a GPU effect surface from the previous offscreen capture.
                            TrackInfoBubble.Effect = TrackInfoBubble.Effect?.CloneCurrentValue();
                            MediaControls.Effect = MediaControls.Effect?.CloneCurrentValue();
                            TrackInfoBubble.InvalidateVisual();
                            MediaControls.InvalidateVisual();
                            await Task.Delay(80);
                            _trackInfoMotion.Show(animate: false);
                            _mediaControlsMotion.Show(animate: false);
                            CaptureQuickActionsQa(this, Path.Combine(directory, id + ".png"));
                        }
                    }
                }
            }
            // Every registered animation, including the widest long-idle crop.
            SetDisplayScalePercent(100, save: false);
            Left = GetCurrentWorkArea().Left + 300;
            Top = GetCurrentWorkArea().Top + 200;
            foreach (var asset in _animationCatalog.Assets)
            {
                double x = Left, y = Top;
                PlayAnimation(asset.Id);
                UpdateLayout();
                Check(Near(Left, x) && Near(Top, y) && Near(TrackInfoBubble.Width, 226), "Catalog: " + asset.Id);
                await Task.Delay(10);
            }
            foreach (string style in new[] { AppearanceOptionIds.FullBodyCrystalDress, AppearanceOptionIds.FullBodyClassicCatEars })
            {
                ApplyAppearancePreferences(_settings.Appearance with { FullBodyStyle = style });
                double x = Left, y = Top;
                foreach (PetContinuousState state in new[] { PetContinuousState.MusicPlaying, PetContinuousState.Idle,
                    PetContinuousState.MusicPlaying, PetContinuousState.Idle })
                {
                    _stateMachine.SetContinuousState(state);
                    await TransitionToResolvedContinuousAnimationAsync("qa.stable_layout_transition");
                    Check(Near(Left, x) && Near(Top, y), $"Real transition pipeline: {style} {state}");
                }
            }
            foreach (EdgeDockSide side in new[] { EdgeDockSide.Left, EdgeDockSide.Right, EdgeDockSide.Bottom })
            {
                _dragEdgeCandidate = side;
                Check(TryEnterEdgeDock(), "Explicit edge docking still starts: " + side);
                await Task.Delay(1200);
                var work = GetCurrentWorkArea();
                var handle = GetPetImageVisibleBoundsInWindow();
                Check(Left + handle.Right >= work.Left - 1 && Left + handle.Left <= work.Right + 1 &&
                    Top + handle.Bottom >= work.Top - 1 && Top + handle.Top <= work.Bottom + 1,
                    "Edge handle remains reachable: " + side);
                ShowPetFromTray();
                Check(_edgeDockSide == EdgeDockSide.None && IsVisible, "Recall exits edge docking: " + side);
            }
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
