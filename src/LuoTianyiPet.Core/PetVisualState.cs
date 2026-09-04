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
    MusicPaused,
    Sleeping,
    Dragging,
    HiddenForSafety,
}

public sealed record PetVisualState(
    PetDisplayMode SelectedDisplayMode = PetDisplayMode.Compact,
    PetContinuousState ContinuousState = PetContinuousState.Idle,
    string MusicAnimationId = "ninth-anniversary-music-sway",
    string FullBodyAnimationId = "official-v4-chibi-full-body-idle")
{
    public const string CompactIdleAnimation = "user-chibi-compact-idle";
    public const string FullBodyIdleAnimation = "official-v4-chibi-full-body-idle";
    public const string MusicSwayAnimation = "ninth-anniversary-music-sway";
    public const string EnjoyMusicAnimation = "resonance-enjoy-music";
    public const string MusicPausedAnimation = "ninth-anniversary-not-awake-breathing";
    public const string SleepingAnimation = "tenth-anniversary-goodnight-float";
    public const string CompactDraggingAnimation = "resonance-expand";
    public const string MediumIdleAnimation = "resonance-hehe";

    public string ResolveContinuousAnimation() => ContinuousState switch
    {
        PetContinuousState.Idle => SelectedDisplayMode switch
        {
            PetDisplayMode.Compact => CompactIdleAnimation,
            PetDisplayMode.FullBodyInteractive => FullBodyAnimationId,
            _ => throw new ArgumentOutOfRangeException(nameof(SelectedDisplayMode)),
        },
        PetContinuousState.MusicPlaying => MusicAnimationId,
        PetContinuousState.MusicPaused => MusicPausedAnimation,
        PetContinuousState.MediumIdle => MediumIdleAnimation,
        PetContinuousState.Sleeping => SleepingAnimation,
        PetContinuousState.Dragging => SelectedDisplayMode == PetDisplayMode.FullBodyInteractive
            ? FullBodyAnimationId
            : CompactDraggingAnimation,
        PetContinuousState.HiddenForSafety => throw new InvalidOperationException(
            "Hidden pets do not have a continuous animation."),
        _ => throw new ArgumentOutOfRangeException(nameof(ContinuousState)),
    };
}
