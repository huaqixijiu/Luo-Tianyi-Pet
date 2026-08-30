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
            (EdgeDockSide.Left, Math.Max(0, window.Left - workArea.Left)),
            (EdgeDockSide.Right, Math.Max(0, workArea.Right - window.Right)),
            (EdgeDockSide.Bottom, Math.Max(0, workArea.Bottom - window.Bottom)),
        ];
        (EdgeDockSide side, double distance) = candidates.MinBy(candidate => candidate.Distance);
        return distance <= threshold ? side : EdgeDockSide.None;
    }
}
