using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class DragReleasePlacementTests
{
    private static readonly DesktopRectangle Work = new(0, 0, 1920, 1080);
    private static readonly DesktopRectangle Idle = new(130, 60, 330, 460);

    [Theory]
    [InlineData(1550, 200, 370, 370, 1460, 200)] // narrower idle keeps right contact
    [InlineData(0, 200, 370, 370, -130, 200)]
    [InlineData(800, 0, 370, 370, 750, -60)]
    [InlineData(800, 710, 370, 370, 750, 560)]
    [InlineData(1550, 0, 370, 370, 1460, -60)]
    [InlineData(0, 710, 370, 370, -130, 560)]
    [InlineData(1580, 200, 370, 370, 1460, 200)] // partial overscan below hiding threshold
    public void RestoredArtworkRetainsContactOnEachAxis(
        double x, double y, double width, double height, double left, double top)
    {
        PointerPoint result = DragReleasePlacement.Resolve(new(750, 200),
            new(x, y, width, height), Idle, Work, 1);
        Assert.Equal(new PointerPoint(left, top), result);
    }

    [Fact]
    public void NoContactPreservesPositionDespiteTransparentPadding()
    {
        PointerPoint desired = new(1200, 200);
        Assert.Equal(desired, DragReleasePlacement.Resolve(desired,
            new(1300, 260, 370, 370), Idle, Work, 1));
    }

    [Fact]
    public void LargerRestoredArtworkIsClampedOnlyWhenItActuallyOverflows()
    {
        Assert.Equal(new PointerPoint(1460, 200), DragReleasePlacement.Resolve(new(1500, 200),
            new(1600, 260, 290, 370), Idle, Work, 1));
    }

    [Theory]
    [InlineData(-1920, -200)]
    [InlineData(1920, 300)]
    public void MonitorOriginAndInsetsAreRespected(double x, double y)
    {
        DesktopRectangle work = new(x + 40, y + 30, 1800, 980);
        PointerPoint result = DragReleasePlacement.Resolve(new(x + 700, y + 200),
            new(work.Right - 370, work.Top, 370, 370), Idle, work, 1);
        Assert.Equal(work.Right, result.X + Idle.Right);
        Assert.Equal(work.Top, result.Y + Idle.Top);
    }

    [Fact]
    public void OversizedTargetKeepsItsStartReachableOnSmallWorkArea()
    {
        Assert.Equal(new PointerPoint(-130, -60), DragReleasePlacement.Resolve(new(0, 0),
            new(0, 0, 370, 370), Idle, new(0, 0, 250, 300), 1));
    }
}
