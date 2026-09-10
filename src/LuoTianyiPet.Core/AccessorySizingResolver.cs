namespace LuoTianyiPet.Core;

public readonly record struct AccessorySizing(
    double TrackInfoWidth,
    double MediaControlsScale);

/// <summary>
/// Keeps the music islands readable without letting them inherit every change
/// in the pet artwork width.
/// </summary>
public static class AccessorySizingResolver
{
    public const double HorizontalInset = 10;
    public const double MediaControlsNaturalWidth = 178;
    public const double MaximumTrackInfoWidth = 240;

    public static AccessorySizing Resolve(double windowWidth)
    {
        if (!double.IsFinite(windowWidth) || windowWidth <= HorizontalInset)
        {
            throw new ArgumentOutOfRangeException(nameof(windowWidth));
        }

        double availableWidth = windowWidth - HorizontalInset;
        return new AccessorySizing(
            Math.Min(availableWidth, MaximumTrackInfoWidth),
            Math.Min(1, availableWidth / MediaControlsNaturalWidth));
    }
}
