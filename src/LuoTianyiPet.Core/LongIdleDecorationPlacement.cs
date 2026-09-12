namespace LuoTianyiPet.Core;

public readonly record struct LongIdleDecorationPlacement(
    double Left,
    double Top,
    bool MirrorHorizontally);

public static class LongIdleDecorationPlacementResolver
{
    public static LongIdleDecorationPlacement Resolve(
        DesktopRectangle frameOnDesktop,
        DesktopRectangle workArea,
        double decorationWidth,
        double decorationHeight,
        PointerPoint rightTarget,
        PointerPoint leftTarget,
        PointerPoint contentOrigin)
    {
        if (frameOnDesktop.Width <= 0 || frameOnDesktop.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameOnDesktop));
        }

        if (decorationWidth <= 0 || decorationHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decorationWidth));
        }

        ValidateNormalizedOrigin(contentOrigin);
        double rightLeft = rightTarget.X - contentOrigin.X * decorationWidth;
        double rightTop = rightTarget.Y - contentOrigin.Y * decorationHeight;
        double leftLeft = leftTarget.X - (1 - contentOrigin.X) * decorationWidth;
        double leftTop = leftTarget.Y - contentOrigin.Y * decorationHeight;
        double rightOverflow = HorizontalOverflow(
            frameOnDesktop.Left + rightLeft,
            decorationWidth,
            workArea);
        double leftOverflow = HorizontalOverflow(
            frameOnDesktop.Left + leftLeft,
            decorationWidth,
            workArea);

        if (rightOverflow <= 0.5 || rightOverflow <= leftOverflow)
        {
            return new LongIdleDecorationPlacement(rightLeft, rightTop, false);
        }

        return new LongIdleDecorationPlacement(leftLeft, leftTop, true);
    }

    private static double HorizontalOverflow(
        double left,
        double width,
        DesktopRectangle workArea) =>
        Math.Max(0, workArea.Left - left) +
        Math.Max(0, left + width - workArea.Right);

    private static void ValidateNormalizedOrigin(PointerPoint origin)
    {
        if (origin.X is < 0 or > 1 || origin.Y is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }
    }
}
