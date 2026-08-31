using LuoTianyiPet.Animation;

namespace LuoTianyiPet.Animation.Tests;

public sealed class EdgeDockFrameRouteResolverTests
{
    [Theory]
    [InlineData(null, 3, 19)]
    [InlineData(3, 3, 19)]
    [InlineData(6, 6, 19)]
    [InlineData(19, 19, 19)]
    public void Resolve_RevealContinuesToTheFullEffect(
        int? current,
        int expectedStart,
        int expectedEnd)
    {
        EdgeDockFrameRoute route = EdgeDockFrameRouteResolver.Resolve(
            reveal: true,
            current,
            hiddenFrameIndex: 3,
            hideStartFrameIndex: 7,
            revealEndFrameIndex: 19);

        Assert.Equal(new EdgeDockFrameRoute(expectedStart, expectedEnd), route);
    }

    [Theory]
    [InlineData(null, 7, 3)]
    [InlineData(7, 7, 3)]
    [InlineData(5, 5, 3)]
    [InlineData(19, 7, 3)]
    public void Resolve_HideUsesOnlyTheCleanNoTextSegment(
        int? current,
        int expectedStart,
        int expectedEnd)
    {
        EdgeDockFrameRoute route = EdgeDockFrameRouteResolver.Resolve(
            reveal: false,
            current,
            hiddenFrameIndex: 3,
            hideStartFrameIndex: 7,
            revealEndFrameIndex: 19);

        Assert.Equal(new EdgeDockFrameRoute(expectedStart, expectedEnd), route);
    }

    [Fact]
    public void Resolve_BottomHideCanStartBeforeItsRevealEnd()
    {
        EdgeDockFrameRoute route = EdgeDockFrameRouteResolver.Resolve(
            reveal: false,
            currentFrameIndex: 7,
            hiddenFrameIndex: 3,
            hideStartFrameIndex: 5,
            revealEndFrameIndex: 7);

        Assert.Equal(new EdgeDockFrameRoute(5, 3), route);
    }

    [Theory]
    [InlineData(-1, 3, 7)]
    [InlineData(3, 2, 7)]
    [InlineData(3, 7, 6)]
    public void Resolve_RejectsInvalidFrameOrdering(
        int hidden,
        int hideStart,
        int revealEnd)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeDockFrameRouteResolver.Resolve(
                reveal: true,
                currentFrameIndex: null,
                hidden,
                hideStart,
                revealEnd));
    }
}
