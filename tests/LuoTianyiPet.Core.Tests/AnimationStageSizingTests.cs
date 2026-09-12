using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class AnimationStageSizingTests
{
    [Theory]
    [InlineData(50, 138, 245, 116)]
    [InlineData(100, 260, 364, 226)]
    [InlineData(150, 382, 483, 240)]
    [InlineData(200, 504, 602, 240)]
    public void UserScaleDefinesStageAndIslands(int scale, double width, double height, double track)
    {
        AnimationStageSizing size = AnimationStageSizing.Resolve(244, 238, scale);
        Assert.Equal(width, size.Width);
        Assert.Equal(height, size.Height);
        Assert.Equal(track, size.Accessories.TrackInfoWidth);
        Assert.True(size.Accessories.MediaControlsScale * 178 <= size.Width - 10);
    }

    [Fact]
    public void WiderFutureArtworkDoesNotEnlargeTheIslands()
    {
        AnimationStageSizing standard = AnimationStageSizing.Resolve(244, 238, 100);
        AnimationStageSizing wide = AnimationStageSizing.Resolve(320, 300, 100);
        Assert.Equal(standard.Accessories, wide.Accessories);
        Assert.Equal(336, wide.Width);
        Assert.Equal(426, wide.Height);
    }

    [Theory]
    [InlineData(0, 238, 100)]
    [InlineData(244, 0, 100)]
    [InlineData(double.NaN, 238, 100)]
    [InlineData(244, 238, 49)]
    [InlineData(244, 238, 201)]
    public void InvalidGeometryIsRejected(double width, double height, int scale) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AnimationStageSizing.Resolve(width, height, scale));
}
