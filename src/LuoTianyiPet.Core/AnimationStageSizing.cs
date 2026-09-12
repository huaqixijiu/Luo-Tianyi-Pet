namespace LuoTianyiPet.Core;

public readonly record struct AnimationStageSizing(double Width, double Height, AccessorySizing Accessories)
{
    // Size the transparent stage once per user scale, not once per animation.
    // Artwork keeps its own display dimensions and is bottom-centred within it.
    public static AnimationStageSizing Resolve(double maximumArtworkWidth, double maximumArtworkHeight, int scalePercent)
    {
        if (!Numeric.IsFinite(maximumArtworkWidth) || maximumArtworkWidth <= 0 ||
            !Numeric.IsFinite(maximumArtworkHeight) || maximumArtworkHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumArtworkWidth));
        if (scalePercent < 50 || scalePercent > 200)
            throw new ArgumentOutOfRangeException(nameof(scalePercent));
        double scale = scalePercent / 100.0;
        return new AnimationStageSizing(
            Math.Max(220, maximumArtworkWidth) * scale + 16,
            maximumArtworkHeight * scale + 126,
            AccessorySizingResolver.Resolve(220 * scale + 16));
    }
}
