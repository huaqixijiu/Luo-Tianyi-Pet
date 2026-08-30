using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class EdgeDockResolverTests
{
    private static readonly DesktopRectangle WorkArea = new(0, 0, 1920, 1040);

    [Theory]
    [InlineData(0, 300, EdgeDockSide.Left)]
    [InlineData(1720, 300, EdgeDockSide.Right)]
    [InlineData(800, 840, EdgeDockSide.Bottom)]
    public void ResolvesSupportedEdges(double left, double top, EdgeDockSide expected)
    {
        DesktopRectangle window = new(left, top, 200, 200);

        Assert.Equal(expected, EdgeDockResolver.Resolve(window, WorkArea, 18));
    }

    [Fact]
    public void DoesNotDockAtTopOrAwayFromEdges()
    {
        Assert.Equal(
            EdgeDockSide.None,
            EdgeDockResolver.Resolve(new DesktopRectangle(800, 0, 200, 200), WorkArea, 18));
        Assert.Equal(
            EdgeDockSide.None,
            EdgeDockResolver.Resolve(new DesktopRectangle(800, 400, 200, 200), WorkArea, 18));
    }
}
