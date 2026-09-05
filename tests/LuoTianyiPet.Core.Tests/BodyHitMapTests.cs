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
    [InlineData(0.41, 0.43, BodyRegionId.LeftEye)]
    [InlineData(0.63, 0.43, BodyRegionId.RightEye)]
    [InlineData(0.50, 0.52, BodyRegionId.Mouth)]
    [InlineData(0.50, 0.32, BodyRegionId.Face)]
    [InlineData(0.32, 0.79, BodyRegionId.LeftHand)]
    [InlineData(0.68, 0.79, BodyRegionId.RightHand)]
    [InlineData(0.50, 0.61, BodyRegionId.Chest)]
    [InlineData(0.50, 0.76, BodyRegionId.LowerBodySensitiveArea)]
    [InlineData(0.42, 0.94, BodyRegionId.LeftFoot)]
    [InlineData(0.61, 0.94, BodyRegionId.RightFoot)]
    [InlineData(0.20, 0.18, BodyRegionId.HeadAndHair)]
    [InlineData(0.42, 0.70, BodyRegionId.OtherBody)]
    public void ClassicCatEarsMapMatchesTheCurrentArtwork(
        double x,
        double y,
        BodyRegionId expected)
    {
        Assert.Equal(
            expected,
            BodyHitMap.ClassicCatEars.HitTest(new PointerPoint(x, y)));
    }

    [Theory]
    [InlineData(0.40, 0.42, BodyRegionId.LeftEye)]
    [InlineData(0.63, 0.42, BodyRegionId.RightEye)]
    [InlineData(0.515, 0.515, BodyRegionId.Mouth)]
    [InlineData(0.40, 0.50, BodyRegionId.Face)]
    [InlineData(0.34, 0.75, BodyRegionId.LeftHand)]
    [InlineData(0.68, 0.75, BodyRegionId.RightHand)]
    [InlineData(0.51, 0.64, BodyRegionId.Chest)]
    [InlineData(0.51, 0.79, BodyRegionId.LowerBodySensitiveArea)]
    [InlineData(0.42, 0.90, BodyRegionId.LeftFoot)]
    [InlineData(0.58, 0.90, BodyRegionId.RightFoot)]
    [InlineData(0.50, 0.20, BodyRegionId.HeadAndHair)]
    [InlineData(0.45, 0.72, BodyRegionId.OtherBody)]
    public void ExportedClassicCatEarsMapUsesUserPolygons(
        double x,
        double y,
        BodyRegionId expected)
    {
        BodyHitMap map = LoadExportedClassicMap();

        Assert.Equal(expected, map.HitTest(new PointerPoint(x, y)));
    }

    [Theory]
    [InlineData(0.80, 0.10)]
    [InlineData(0.72, 0.55)]
    [InlineData(0.20, 0.70)]
    public void ExportedClassicCatEarsMapRejectsPointsOutsideDrawnPolygons(double x, double y)
    {
        BodyHitMap map = LoadExportedClassicMap();

        Assert.Null(map.HitTest(new PointerPoint(x, y)));
    }

    [Theory]
    [InlineData(0.40, 0.39, BodyRegionId.LeftEye)]
    [InlineData(0.61, 0.39, BodyRegionId.RightEye)]
    [InlineData(0.50, 0.46, BodyRegionId.Mouth)]
    [InlineData(0.39, 0.455, BodyRegionId.Face)]
    [InlineData(0.35, 0.68, BodyRegionId.LeftHand)]
    [InlineData(0.65, 0.68, BodyRegionId.RightHand)]
    [InlineData(0.51, 0.57, BodyRegionId.Chest)]
    [InlineData(0.50, 0.76, BodyRegionId.LowerBodySensitiveArea)]
    [InlineData(0.45, 0.90, BodyRegionId.LeftFoot)]
    [InlineData(0.56, 0.90, BodyRegionId.RightFoot)]
    [InlineData(0.50, 0.20, BodyRegionId.HeadAndHair)]
    [InlineData(0.50, 0.68, BodyRegionId.OtherBody)]
    public void ExportedCrystalDressMapUsesUserPolygons(
        double x,
        double y,
        BodyRegionId expected)
    {
        BodyHitMap map = LoadExportedCrystalMap();

        Assert.Equal(expected, map.HitTest(new PointerPoint(x, y)));
    }

    [Theory]
    [InlineData(0.10, 0.10)]
    [InlineData(0.85, 0.50)]
    [InlineData(0.30, 0.80)]
    public void ExportedCrystalDressMapRejectsPointsOutsideDrawnPolygons(double x, double y)
    {
        BodyHitMap map = LoadExportedCrystalMap();

        Assert.Null(map.HitTest(new PointerPoint(x, y)));
    }

    [Fact]
    public void PolygonHitTestDoesNotUseItsBoundingRectangleAsTheRegion()
    {
        BodyHitMap map = new(
        [
            BodyHitRegion.FromPolygons(
                BodyRegionId.OtherBody,
                [
                    new NormalizedPolygon(
                    [
                        new(0.1, 0.1),
                        new(0.9, 0.1),
                        new(0.1, 0.9),
                    ]),
                ]),
        ]);

        Assert.Equal(BodyRegionId.OtherBody, map.HitTest(new PointerPoint(0.2, 0.2)));
        Assert.Null(map.HitTest(new PointerPoint(0.8, 0.8)));
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

    private static BodyHitMap LoadExportedClassicMap()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "full-body-classic.json");
        return BodyHitMapJsonParser.Parse(
            File.ReadAllText(path),
            AppearanceOptionIds.ClassicCatEarsAnimation);
    }

    private static BodyHitMap LoadExportedCrystalMap()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "full-body-crystal.json");
        return BodyHitMapJsonParser.Parse(
            File.ReadAllText(path),
            AppearanceOptionIds.CrystalDressAnimation);
    }
}
