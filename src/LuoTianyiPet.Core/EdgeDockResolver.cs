namespace LuoTianyiPet.Core;

public enum EdgeDockSide
{
    None,
    Left,
    Right,
    Bottom,
}

public readonly record struct DesktopRectangle(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

public static class EdgeDockResolver
{
    public static EdgeDockSide ResolveHideIntent(
        DesktopRectangle visiblePet,
        DesktopRectangle workArea,
        double activationDepth)
    {
        if (activationDepth < 0 || !double.IsFinite(activationDepth))
        {
            throw new ArgumentOutOfRangeException(nameof(activationDepth));
        }

        (EdgeDockSide Side, double Depth)[] candidates =
        [
            (EdgeDockSide.Left, workArea.Left - visiblePet.Left),
            (EdgeDockSide.Right, visiblePet.Right - workArea.Right),
            (EdgeDockSide.Bottom, visiblePet.Bottom - workArea.Bottom),
        ];
        (EdgeDockSide side, double depth) = candidates.MaxBy(candidate => candidate.Depth);
        return depth >= activationDepth ? side : EdgeDockSide.None;
    }

    public static bool IsNearBottom(
        DesktopRectangle visiblePet,
        DesktopRectangle workArea,
        double distance)
    {
        if (distance < 0 || !double.IsFinite(distance))
        {
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        return visiblePet.Bottom >= workArea.Bottom - distance;
    }
}
