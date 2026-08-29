namespace LuoTianyiPet.Core;

public enum PetDisplayMode
{
    Compact,
    FullBodyInteractive,
}

public sealed record PetVisualState(
    PetDisplayMode SelectedDisplayMode = PetDisplayMode.Compact,
    bool IsMusicPlaying = false)
{
    public const string CompactIdleAnimation = "resonance-hehe";
    public const string FullBodyIdleAnimation = "official-v4-chibi-full-body-idle";
    public const string MusicPlayingAnimation = "ninth-anniversary-music-sway";

    public string ResolveContinuousAnimation() => IsMusicPlaying
        ? MusicPlayingAnimation
        : SelectedDisplayMode switch
        {
            PetDisplayMode.Compact => CompactIdleAnimation,
            PetDisplayMode.FullBodyInteractive => FullBodyIdleAnimation,
            _ => throw new ArgumentOutOfRangeException(nameof(SelectedDisplayMode)),
        };
}
