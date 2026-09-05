using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class EdgeDockResolverTests
{
    private static readonly DesktopRectangle WorkArea = new(0, 0, 1920, 1040);

    [Theory]
    [InlineData(-28, 300, EdgeDockSide.Left)]
    [InlineData(1748, 300, EdgeDockSide.Right)]
    [InlineData(800, 868, EdgeDockSide.Bottom)]
    public void ResolvesExplicitOvershootAsHideIntent(
        double left,
        double top,
        EdgeDockSide expected)
    {
        DesktopRectangle window = new(left, top, 200, 200);

        Assert.Equal(expected, EdgeDockResolver.ResolveHideIntent(window, WorkArea, 28));
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(1720, 300)]
    [InlineData(800, 840)]
    [InlineData(800, 400)]
    public void TouchingOrStayingNearAnEdgeDoesNotHide(double left, double top)
    {
        Assert.Equal(
            EdgeDockSide.None,
            EdgeDockResolver.ResolveHideIntent(
                new DesktopRectangle(left, top, 200, 200),
                WorkArea,
                28));
    }

    [Theory]
    [InlineData(-60, 300, EdgeDockSide.Left)]
    [InlineData(1800, 300, EdgeDockSide.Right)]
    [InlineData(800, 900, EdgeDockSide.Bottom)]
    public void ChoosesTheDeepestOvershotEdge(
        double left,
        double top,
        EdgeDockSide expected)
    {
        Assert.Equal(
            expected,
            EdgeDockResolver.ResolveHideIntent(
                new DesktopRectangle(left, top, 200, 200),
                WorkArea,
                28));
    }

    [Theory]
    [InlineData(-20, 300, EdgeDockSide.Left)]
    [InlineData(1740, 300, EdgeDockSide.Right)]
    [InlineData(800, 860, EdgeDockSide.Bottom)]
    public void KeepsCurrentIntentInsideReleaseHysteresisBand(
        double left,
        double top,
        EdgeDockSide currentIntent)
    {
        EdgeDockSide result = EdgeDockResolver.ResolveHideIntentWithHysteresis(
            new DesktopRectangle(left, top, 200, 200),
            WorkArea,
            activationDepth: 28,
            releaseDepth: 14,
            currentIntent);

        Assert.Equal(currentIntent, result);
    }

    [Fact]
    public void ClearsCurrentIntentAfterPointerRetreatsPastReleaseDepth()
    {
        EdgeDockSide result = EdgeDockResolver.ResolveHideIntentWithHysteresis(
            new DesktopRectangle(-13, 300, 200, 200),
            WorkArea,
            activationDepth: 28,
            releaseDepth: 14,
            EdgeDockSide.Left);

        Assert.Equal(EdgeDockSide.None, result);
    }

    [Theory]
    [InlineData(-67, 300, 200, 200, EdgeDockSide.Left)]
    [InlineData(1787, 300, 200, 200, EdgeDockSide.Right)]
    [InlineData(800, 907, 200, 200, EdgeDockSide.Bottom)]
    [InlineData(-66, 300, 200, 200, EdgeDockSide.None)]
    [InlineData(-133, 300, 400, 400, EdgeDockSide.None)]
    [InlineData(-134, 300, 400, 400, EdgeDockSide.Left)]
    public void FractionalHideIntentRequiresOneThirdOfVisibleArtworkOutside(
        double left,
        double top,
        double width,
        double height,
        EdgeDockSide expected)
    {
        Assert.Equal(
            expected,
            EdgeDockResolver.ResolveHideIntentByFraction(
                new DesktopRectangle(left, top, width, height),
                WorkArea,
                1.0 / 3.0));
    }

    [Fact]
    public void FractionalHysteresisKeepsIntentUntilOnlyOneQuarterRemainsOutside()
    {
        Assert.Equal(
            EdgeDockSide.Left,
            EdgeDockResolver.ResolveHideIntentByFractionWithHysteresis(
                new DesktopRectangle(-51, 300, 200, 200),
                WorkArea,
                1.0 / 3.0,
                0.25,
                EdgeDockSide.Left));
        Assert.Equal(
            EdgeDockSide.None,
            EdgeDockResolver.ResolveHideIntentByFractionWithHysteresis(
                new DesktopRectangle(-49, 300, 200, 200),
                WorkArea,
                1.0 / 3.0,
                0.25,
                EdgeDockSide.Left));
    }

    [Theory]
    [InlineData(740, true)]
    [InlineData(768, true)]
    [InlineData(739, false)]
    public void DetectsWhenControlsNeedTheAbovePetLayout(double top, bool expected)
    {
        Assert.Equal(
            expected,
            EdgeDockResolver.IsNearBottom(
                new DesktopRectangle(800, top, 200, 200),
                WorkArea,
                100));
    }
}
