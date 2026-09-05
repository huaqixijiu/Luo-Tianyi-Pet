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
    public static EdgeDockSide ResolveHideIntentByFraction(
        DesktopRectangle visiblePet,
        DesktopRectangle workArea,
        double activationFraction)
    {
        ValidateFraction(activationFraction, nameof(activationFraction));
        if (visiblePet.Width <= 0 || visiblePet.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visiblePet));
        }

        (EdgeDockSide Side, double Fraction)[] candidates =
        [
            (EdgeDockSide.Left, (workArea.Left - visiblePet.Left) / visiblePet.Width),
            (EdgeDockSide.Right, (visiblePet.Right - workArea.Right) / visiblePet.Width),
            (EdgeDockSide.Bottom, (visiblePet.Bottom - workArea.Bottom) / visiblePet.Height),
        ];
        (EdgeDockSide side, double fraction) = candidates.MaxBy(candidate => candidate.Fraction);
        return fraction >= activationFraction ? side : EdgeDockSide.None;
    }

    public static EdgeDockSide ResolveHideIntentByFractionWithHysteresis(
        DesktopRectangle visiblePet,
        DesktopRectangle workArea,
        double activationFraction,
        double releaseFraction,
        EdgeDockSide currentIntent)
    {
        ValidateFraction(activationFraction, nameof(activationFraction));
        ValidateFraction(releaseFraction, nameof(releaseFraction));
        if (releaseFraction > activationFraction)
        {
            throw new ArgumentOutOfRangeException(nameof(releaseFraction));
        }

        if (currentIntent == EdgeDockSide.None)
        {
            return ResolveHideIntentByFraction(
                visiblePet,
                workArea,
                activationFraction);
        }

        double currentFraction = currentIntent switch
        {
            EdgeDockSide.Left => (workArea.Left - visiblePet.Left) / visiblePet.Width,
            EdgeDockSide.Right => (visiblePet.Right - workArea.Right) / visiblePet.Width,
            EdgeDockSide.Bottom => (visiblePet.Bottom - workArea.Bottom) / visiblePet.Height,
            _ => throw new ArgumentOutOfRangeException(nameof(currentIntent)),
        };
        if (currentFraction >= releaseFraction)
        {
            return currentIntent;
        }

        return ResolveHideIntentByFraction(
            visiblePet,
            workArea,
            activationFraction);
    }

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

    public static EdgeDockSide ResolveHideIntentWithHysteresis(
        DesktopRectangle visiblePet,
        DesktopRectangle workArea,
        double activationDepth,
        double releaseDepth,
        EdgeDockSide currentIntent)
    {
        if (releaseDepth < 0 || !double.IsFinite(releaseDepth) || releaseDepth > activationDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(releaseDepth));
        }

        EdgeDockSide resolved = ResolveHideIntent(visiblePet, workArea, activationDepth);
        if (resolved != EdgeDockSide.None || currentIntent == EdgeDockSide.None)
        {
            return resolved;
        }

        double currentDepth = currentIntent switch
        {
            EdgeDockSide.Left => workArea.Left - visiblePet.Left,
            EdgeDockSide.Right => visiblePet.Right - workArea.Right,
            EdgeDockSide.Bottom => visiblePet.Bottom - workArea.Bottom,
            _ => throw new ArgumentOutOfRangeException(nameof(currentIntent)),
        };
        return currentDepth >= releaseDepth ? currentIntent : EdgeDockSide.None;
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

    public static bool IsNearTop(
        DesktopRectangle visiblePet,
        DesktopRectangle workArea,
        double distance)
    {
        if (distance < 0 || !double.IsFinite(distance))
        {
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        return visiblePet.Top <= workArea.Top + distance;
    }

    private static void ValidateFraction(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
