namespace LuoTianyiPet.Core;

public enum PetDisplayMode
{
    Compact,
    FullBodyInteractive,
}

public enum PetContinuousState
{
    Idle,
    MediumIdle,
    MusicPlaying,
    Sleeping,
    Dragging,
    HiddenForSafety,
}

public sealed record PetVisualState(
    PetDisplayMode SelectedDisplayMode = PetDisplayMode.Compact,
    PetContinuousState ContinuousState = PetContinuousState.Idle,
    string MusicAnimationId = "ninth-anniversary-music-sway")
{
    public const string CompactIdleAnimation = "resonance-hehe";
    public const string FullBodyIdleAnimation = "official-v4-chibi-full-body-idle";
    public const string MusicSwayAnimation = "ninth-anniversary-music-sway";
    public const string EnjoyMusicAnimation = "resonance-enjoy-music";
    public const string SleepingAnimation = "tenth-anniversary-goodnight-float";
    public const string CompactDraggingAnimation = "resonance-expand";
    public const string MediumIdleAnimation = "twelfth-anniversary-lie-flat";

    public string ResolveContinuousAnimation() => ContinuousState switch
    {
        PetContinuousState.Idle => SelectedDisplayMode switch
        {
            PetDisplayMode.Compact => CompactIdleAnimation,
            PetDisplayMode.FullBodyInteractive => FullBodyIdleAnimation,
            _ => throw new ArgumentOutOfRangeException(nameof(SelectedDisplayMode)),
        },
        PetContinuousState.MusicPlaying => MusicAnimationId,
        PetContinuousState.MediumIdle => MediumIdleAnimation,
        PetContinuousState.Sleeping => SleepingAnimation,
        PetContinuousState.Dragging => SelectedDisplayMode == PetDisplayMode.FullBodyInteractive
            ? FullBodyIdleAnimation
            : CompactDraggingAnimation,
        PetContinuousState.HiddenForSafety => throw new InvalidOperationException(
            "Hidden pets do not have a continuous animation."),
        _ => throw new ArgumentOutOfRangeException(nameof(ContinuousState)),
    };
}
