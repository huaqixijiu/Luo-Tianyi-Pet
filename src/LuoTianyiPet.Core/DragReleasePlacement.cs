namespace LuoTianyiPet.Core;

public static class DragReleasePlacement
{
    // Source is the visible artwork at release in desktop coordinates. Target is
    // the restored artwork in window coordinates, never the transparent stage.
    public static PointerPoint Resolve(
        PointerPoint windowPosition,
        DesktopRectangle source,
        DesktopRectangle target,
        DesktopRectangle workArea,
        double edgeTolerance)
    {
        double left = ResolveAxis(windowPosition.X, source.Left, source.Right,
            target.Left, target.Right, workArea.Left, workArea.Right, edgeTolerance);
        double top = ResolveAxis(windowPosition.Y, source.Top, source.Bottom,
            target.Top, target.Bottom, workArea.Top, workArea.Bottom, edgeTolerance);
        return new PointerPoint(left, top);
    }

    private static double ResolveAxis(double position, double sourceStart, double sourceEnd,
        double targetStart, double targetEnd, double areaStart, double areaEnd, double tolerance)
    {
        double minimum = areaStart - targetStart;
        double maximum = Math.Max(minimum, areaEnd - targetEnd);
        if (sourceStart <= areaStart + tolerance) return minimum;
        if (sourceEnd >= areaEnd - tolerance) return maximum;
        return Numeric.Clamp(position, minimum, maximum);
    }
}
