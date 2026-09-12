using Xunit;
namespace LuoTianyiPet.Core.Tests;

public class MessageSidePlacementTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void RightEdgeMirrorsCharacterAndFitsCard(double dpi)
    {
        var p = MessageSidePlacement.Resolve(new(1680*dpi, 400*dpi, 230*dpi, 238*dpi),
            212*dpi, 82*dpi, new(0,0,1920*dpi,1040*dpi), 4*dpi, 8*dpi);
        Assert.True(p.MirrorCharacter);
        Assert.True(p.Left + 212*dpi < 1680*dpi);
        Assert.InRange(p.Top, 8*dpi, (1040-82-8)*dpi);
    }
    [Fact]
    public void CenterUsesRightSideWithoutMovingCharacter()
    {
        var p = MessageSidePlacement.Resolve(new(600,400,244,238),212,82,new(0,0,1920,1040));
        Assert.False(p.MirrorCharacter);
        Assert.Equal(848,p.Left);
    }
    [Fact]
    public void NegativeMonitorAndBottomEdgeRemainInsideWorkArea()
    {
        var p = MessageSidePlacement.Resolve(new(-260,910,244,238),212,82,new(-1920,0,1920,1040));
        Assert.True(p.MirrorCharacter);
        Assert.InRange(p.Left,-1912,-220);
        Assert.Equal(950,p.Top);
    }
    [Fact]
    public void NarrowWorkAreaClampsToVisibleSurface()
    {
        var p = MessageSidePlacement.Resolve(new(0,0,244,238),212,82,new(0,0,300,260));
        Assert.InRange(p.Left,8,80);
        Assert.InRange(p.Top,8,170);
    }
}
