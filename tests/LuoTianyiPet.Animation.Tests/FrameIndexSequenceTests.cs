using LuoTianyiPet.Animation;

namespace LuoTianyiPet.Animation.Tests;

public sealed class FrameIndexSequenceTests
{
    [Theory]
    [InlineData(3, 7, new[] { 3, 4, 5, 6, 7 })]
    [InlineData(7, 3, new[] { 7, 6, 5, 4, 3 })]
    [InlineData(5, 5, new[] { 5 })]
    public void Create_TraversesFromCurrentFrameWithoutJump(
        int start,
        int end,
        int[] expected)
    {
        Assert.Equal(expected, FrameIndexSequence.Create(start, end, 20));
    }

    [Theory]
    [InlineData(-1, 3, 20)]
    [InlineData(3, 20, 20)]
    [InlineData(0, 0, 0)]
    public void Create_RejectsFramesOutsideTheAnimation(
        int start,
        int end,
        int frameCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrameIndexSequence.Create(start, end, frameCount));
    }
}
