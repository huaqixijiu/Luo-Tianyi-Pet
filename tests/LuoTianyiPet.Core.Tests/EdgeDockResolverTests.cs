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
    [InlineData(-50, 300, 200, 200, EdgeDockSide.Left)]
    [InlineData(1770, 300, 200, 200, EdgeDockSide.Right)]
    [InlineData(800, 890, 200, 200, EdgeDockSide.Bottom)]
    [InlineData(-49, 300, 200, 200, EdgeDockSide.None)]
    [InlineData(-99, 300, 400, 400, EdgeDockSide.None)]
    [InlineData(-100, 300, 400, 400, EdgeDockSide.Left)]
    public void FractionalHideIntentRequiresOneQuarterOfVisibleArtworkOutside(
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
                0.25));
    }

    [Fact]
    public void FractionalHysteresisKeepsIntentUntilOnlyOneSixthRemainsOutside()
    {
        Assert.Equal(
            EdgeDockSide.Left,
            EdgeDockResolver.ResolveHideIntentByFractionWithHysteresis(
                new DesktopRectangle(-34, 300, 200, 200),
                WorkArea,
                0.25,
                1.0 / 6.0,
                EdgeDockSide.Left));
        Assert.Equal(
            EdgeDockSide.None,
            EdgeDockResolver.ResolveHideIntentByFractionWithHysteresis(
                new DesktopRectangle(-33, 300, 200, 200),
                WorkArea,
                0.25,
                1.0 / 6.0,
                EdgeDockSide.Left));
    }

    [Fact]
    public void FractionalHysteresisDoesNotSwitchSidesWhileCurrentIntentRemainsOutside()
    {
        DesktopRectangle bottomRightCorner = new(1770, 900, 200, 200);

        Assert.Equal(
            EdgeDockSide.Right,
            EdgeDockResolver.ResolveHideIntentByFractionWithHysteresis(
                bottomRightCorner,
                WorkArea,
                0.25,
                1.0 / 6.0,
                EdgeDockSide.Right));
        Assert.Equal(
            EdgeDockSide.Bottom,
            EdgeDockResolver.ResolveHideIntentByFractionWithHysteresis(
                bottomRightCorner,
                WorkArea,
                0.25,
                1.0 / 6.0,
                EdgeDockSide.Bottom));
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

    [Theory]
    [InlineData(0, true)]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public void DetectsWhenControlsNeedTheBelowPetLayout(double top, bool expected)
    {
        Assert.Equal(
            expected,
            EdgeDockResolver.IsNearTop(
                new DesktopRectangle(800, top, 200, 200),
                WorkArea,
                80));
    }
}
