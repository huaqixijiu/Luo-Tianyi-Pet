using LuoTianyiPet.Core;
namespace LuoTianyiPet.Core.Tests;
public sealed class ReminderPlacementTests
{
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
