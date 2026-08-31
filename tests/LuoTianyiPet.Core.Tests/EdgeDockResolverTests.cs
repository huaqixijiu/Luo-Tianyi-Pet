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
