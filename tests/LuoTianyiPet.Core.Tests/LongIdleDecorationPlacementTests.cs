using Xunit;

namespace LuoTianyiPet.Core.Tests;

public sealed class LongIdleDecorationPlacementTests
{
    private static readonly DesktopRectangle WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void ResolveUsesRightHeadAnchorWhenDecorationFits()
    {
        LongIdleDecorationPlacement placement = LongIdleDecorationPlacementResolver.Resolve(
            new DesktopRectangle(800, 300, 300, 238),
            WorkArea,
            108,
            81,
            new PointerPoint(200, 76),
            new PointerPoint(100, 76),
            new PointerPoint(0.08, 0.85));

        Assert.False(placement.MirrorHorizontally);
        Assert.Equal(191.36, placement.Left, 2);
        Assert.Equal(7.15, placement.Top, 2);
    }

    [Fact]
    public void ResolveMirrorsToLeftHeadAnchorAtRightScreenEdge()
    {
        LongIdleDecorationPlacement placement = LongIdleDecorationPlacementResolver.Resolve(
            new DesktopRectangle(1740, 300, 300, 238),
            WorkArea,
            108,
            81,
            new PointerPoint(200, 76),
            new PointerPoint(100, 76),
            new PointerPoint(0.08, 0.85));

        Assert.True(placement.MirrorHorizontally);
        Assert.Equal(0.64, placement.Left, 2);
        Assert.Equal(7.15, placement.Top, 2);
    }

    [Fact]
    public void ResolveKeepsPixelOriginOnSelectedAnchorAfterMirroring()
    {
        const double width = 90;
        const double originX = 49.0 / 180.0;
        LongIdleDecorationPlacement placement = LongIdleDecorationPlacementResolver.Resolve(
            new DesktopRectangle(1740, 300, 300, 238),
            WorkArea,
            width,
            90,
            new PointerPoint(200, 76),
            new PointerPoint(100, 76),
            new PointerPoint(originX, 121.0 / 180.0));

        double mirroredOrigin = placement.Left + (1 - originX) * width;
        Assert.Equal(100, mirroredOrigin, 6);
    }
}
