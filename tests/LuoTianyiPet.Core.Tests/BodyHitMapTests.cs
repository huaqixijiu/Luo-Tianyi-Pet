using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class BodyHitMapTests
{
    [Theory]
    [InlineData(0.40, 0.44, BodyRegionId.LeftEye)]
    [InlineData(0.61, 0.44, BodyRegionId.RightEye)]
    [InlineData(0.50, 0.52, BodyRegionId.FaceAndMouth)]
    [InlineData(0.28, 0.69, BodyRegionId.LeftHand)]
    [InlineData(0.77, 0.69, BodyRegionId.RightHand)]
    [InlineData(0.50, 0.64, BodyRegionId.Chest)]
    [InlineData(0.50, 0.80, BodyRegionId.LowerBodySensitiveArea)]
    [InlineData(0.44, 0.94, BodyRegionId.LeftFoot)]
    [InlineData(0.60, 0.94, BodyRegionId.RightFoot)]
    [InlineData(0.25, 0.15, BodyRegionId.HeadAndHair)]
    [InlineData(0.30, 0.85, BodyRegionId.OtherBody)]
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
