namespace LuoTianyiPet.Core;

public static class VerticalRangeMapper
{
    public static double Resolve(
        double pointerY,
        double surfaceHeight,
        double trackPadding,
        double minimum,
        double maximum)
    {
        if (!double.IsFinite(pointerY) ||
            !double.IsFinite(surfaceHeight) ||
            !double.IsFinite(trackPadding) ||
            !double.IsFinite(minimum) ||
            !double.IsFinite(maximum) ||
            surfaceHeight <= 0 ||
            trackPadding < 0 ||
            maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(surfaceHeight));
        }

        double trackHeight = Math.Max(1, surfaceHeight - (trackPadding * 2));
        double fraction = 1 - Math.Clamp((pointerY - trackPadding) / trackHeight, 0, 1);
        return minimum + ((maximum - minimum) * fraction);
    }
}
