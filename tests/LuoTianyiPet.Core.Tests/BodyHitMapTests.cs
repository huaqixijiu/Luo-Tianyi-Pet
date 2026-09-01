using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class BodyHitMapTests
{
    [Theory]
    [InlineData(0.44, 0.25, BodyRegionId.LeftEye)]
    [InlineData(0.56, 0.25, BodyRegionId.RightEye)]
    [InlineData(0.50, 0.31, BodyRegionId.Mouth)]
    [InlineData(0.50, 0.18, BodyRegionId.Face)]
    [InlineData(0.22, 0.55, BodyRegionId.LeftHand)]
    [InlineData(0.74, 0.55, BodyRegionId.RightHand)]
    [InlineData(0.50, 0.40, BodyRegionId.Chest)]
    [InlineData(0.50, 0.58, BodyRegionId.LowerBodySensitiveArea)]
    [InlineData(0.44, 0.90, BodyRegionId.LeftFoot)]
    [InlineData(0.55, 0.90, BodyRegionId.RightFoot)]
    [InlineData(0.50, 0.08, BodyRegionId.HeadAndHair)]
    [InlineData(0.32, 0.70, BodyRegionId.OtherBody)]
    public void FullBodyMapFindsSmallestPriorityRegion(double x, double y, BodyRegionId expected)
    {
        Assert.Equal(expected, BodyHitMap.FullBodyDefault.HitTest(new PointerPoint(x, y)));
    }

    [Theory]
    [InlineData(-0.1, 0.5)]
    [InlineData(1.1, 0.5)]
    [InlineData(0.5, -0.1)]
    [InlineData(0.5, 1.1)]
    [InlineData(0.02, 0.02)]
    public void OutOfCanvasOrOutsideCharacterReturnsNull(double x, double y)
    {
        Assert.Null(BodyHitMap.FullBodyDefault.HitTest(new PointerPoint(x, y)));
    }
}
