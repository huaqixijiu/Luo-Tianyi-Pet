using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private AnimationStageSizing GetAnimationStageSizing()
    {
        AnimationStageSizing stage = AnimationStageSizing.Resolve(
            _animationCatalog?.Assets.Max(asset => asset.DisplayWidth) ?? 244,
            _animationCatalog?.Assets.Max(asset => asset.DisplayHeight) ?? 238,
            _settings.Appearance.DisplayScalePercent);
        return stage with { Height = stage.Height + _feedbackSlotHeight };
    }

    // Deliberately excludes per-frame alpha and animated render transforms.
    // Those remain authoritative for mouse hits and explicit edge docking only.
    private DesktopRectangle GetStableStageBoundsInWindow() => new(
        8, PetVisual.Margin.Top, Width - 16,
        Height - PetVisual.Margin.Top - PetVisual.Margin.Bottom);

    private DesktopRectangle GetStableStageDesktopBounds()
    {
        DesktopRectangle stage = GetStableStageBoundsInWindow();
        return new DesktopRectangle(Left + stage.Left, Top + stage.Top, stage.Width, stage.Height);
    }
}
