using LuoTianyiPet.Animation;

namespace LuoTianyiPet.Animation.Tests;

public sealed class AnimationFrameTimelineTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(99, 0, false)]
    [InlineData(100, 1, false)]
    [InlineData(299, 1, false)]
    [InlineData(300, 0, false)]
    public void InfiniteTimelineSelectsFrameWithinEachCycle(long milliseconds, int index, bool completed)
    {
        AnimationFrameTimeline timeline = new([100, 200], loopCount: 0);

        PlaybackFrame frame = timeline.GetFrame(TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal(index, frame.Index);
        Assert.Equal(completed, frame.IsCompleted);
    }

    [Fact]
    public void FiniteTimelineCompletesOnLastFrameAfterRequestedLoops()
    {
        AnimationFrameTimeline timeline = new([100, 200], loopCount: 2);

        PlaybackFrame frame = timeline.GetFrame(TimeSpan.FromMilliseconds(600));

        Assert.Equal(1, frame.Index);
        Assert.True(frame.IsCompleted);
    }

    [Fact]
    public void TimelineRejectsInvalidDurations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationFrameTimeline([100, 0], 1));
    }

    [Theory]
    [InlineData(0.7, 429)]
    [InlineData(0.8, 375)]
    [InlineData(1.2, 250)]
    [InlineData(1.3, 231)]
    public void TimelineScalesDurationsForPerPlaybackSpeed(
        double playbackRate,
        long expectedCycleMilliseconds)
    {
        AnimationFrameTimeline timeline = new([100, 200], 1, playbackRate);

        Assert.Equal(expectedCycleMilliseconds, timeline.CycleDurationMilliseconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void TimelineRejectsInvalidPlaybackRates(double playbackRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnimationFrameTimeline([100], 1, playbackRate));
    }
}
