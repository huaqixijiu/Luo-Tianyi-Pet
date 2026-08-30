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
    public static EdgeDockSide Resolve(
        DesktopRectangle window,
        DesktopRectangle workArea,
        double threshold)
    {
        if (threshold < 0 || !double.IsFinite(threshold))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }

        (EdgeDockSide Side, double Distance)[] candidates =
        [
            (EdgeDockSide.Left, Math.Abs(window.Left - workArea.Left)),
            (EdgeDockSide.Right, Math.Abs(window.Right - workArea.Right)),
            (EdgeDockSide.Bottom, Math.Abs(window.Bottom - workArea.Bottom)),
        ];
        (EdgeDockSide side, double distance) = candidates.MinBy(candidate => candidate.Distance);
        return distance <= threshold ? side : EdgeDockSide.None;
    }
}
