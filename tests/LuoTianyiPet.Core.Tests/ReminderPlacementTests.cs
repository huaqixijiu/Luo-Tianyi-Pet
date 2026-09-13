using LuoTianyiPet.Core;
namespace LuoTianyiPet.Core.Tests;
public sealed class ReminderPlacementTests
{
    [Theory][InlineData(1)][InlineData(1.25)][InlineData(1.5)]
    public void CenteredBelowAndFlipsAboveWithinMonitorWorkArea(double scale)
    {
        var work=new DesktopRectangle(-1920/scale,0,1920/scale,1040/scale);
        var pet=new DesktopRectangle(-1100/scale,200/scale,220/scale,260/scale);
        var collapsed=ReminderPlacement.Resolve(pet,work,340/scale,64/scale);
        var expanded=ReminderPlacement.Resolve(pet,work,340/scale,220/scale);
        Assert.Equal(collapsed.Top,expanded.Top);Assert.Equal(pet.Bottom+10,expanded.Top);
        Assert.Equal(pet.Left+pet.Width/2,expanded.Left+expanded.Width/2,6);
        var nearBottom=new DesktopRectangle(pet.Left,work.Bottom-pet.Height-20,pet.Width,pet.Height);
        var above=ReminderPlacement.Resolve(nearBottom,work,340/scale,220/scale);
        Assert.Equal(nearBottom.Top-10,above.Bottom,6);Assert.True(above.Top>=work.Top);
    }
    [Theory]
    [InlineData(0,0,170,180,230,70)]
    [InlineData(1100,0,170,180,360,420)]
    [InlineData(0,600,170,180,360,420)]
    [InlineData(1100,600,170,180,230,70)]
    [InlineData(400,200,220,280,360,420)]
    [InlineData(-1200,0,170,180,230,70)]
    public void CardIsInsideWorkAreaAndOutsidePet(double x,double y,double pw,double ph,double w,double h)
    {
        var work=new DesktopRectangle(x<0?-1280:0,0,1280,800);var pet=new DesktopRectangle(x,y,pw,ph);
        var card=ReminderPlacement.Resolve(pet,work,w,h);
        Assert.True(card.Left>=work.Left&&card.Top>=work.Top&&card.Right<=work.Right&&card.Bottom<=work.Bottom);
        Assert.True(card.Right<=pet.Left||card.Left>=pet.Right||card.Bottom<=pet.Top||card.Top>=pet.Bottom);
    }
}
