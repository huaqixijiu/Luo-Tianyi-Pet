using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class RapidBackAndForthDragTrackerTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 11, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void ThreeFastHorizontalLegsTriggerOnce()
    {
        RapidBackAndForthDragTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 100), Start);

        Assert.False(tracker.Add(new PointerPoint(150, 102), Start.AddMilliseconds(100)));
        Assert.False(tracker.Add(new PointerPoint(95, 99), Start.AddMilliseconds(200)));
        Assert.True(tracker.Add(new PointerPoint(150, 101), Start.AddMilliseconds(300)));
        Assert.False(tracker.Add(new PointerPoint(90, 100), Start.AddMilliseconds(400)));
    }

    [Fact]
    public void SlowBackAndForthDoesNotTrigger()
    {
        RapidBackAndForthDragTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 100), Start);

        Assert.False(tracker.Add(new PointerPoint(150, 100), Start.AddMilliseconds(300)));
        Assert.False(tracker.Add(new PointerPoint(90, 100), Start.AddMilliseconds(600)));
        Assert.False(tracker.Add(new PointerPoint(150, 100), Start.AddMilliseconds(900)));
    }

    [Fact]
    public void SmallPointerJitterDoesNotTrigger()
    {
        RapidBackAndForthDragTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 100), Start);

        Assert.False(tracker.Add(new PointerPoint(108, 100), Start.AddMilliseconds(20)));
        Assert.False(tracker.Add(new PointerPoint(99, 100), Start.AddMilliseconds(40)));
        Assert.False(tracker.Add(new PointerPoint(108, 100), Start.AddMilliseconds(60)));
    }

    [Fact]
    public void ExpiredSequenceStartsOver()
    {
        RapidBackAndForthDragTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 100), Start);
        tracker.Add(new PointerPoint(150, 100), Start.AddMilliseconds(100));

        Assert.False(tracker.Add(new PointerPoint(90, 100), Start.AddSeconds(1)));
        Assert.False(tracker.Add(new PointerPoint(150, 100), Start.AddMilliseconds(1100)));
    }

    [Fact]
    public void EarlierTimestampFailsClosed()
    {
        RapidBackAndForthDragTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 100), Start.AddMilliseconds(10));

        Assert.False(tracker.Add(new PointerPoint(150, 100), Start));
        Assert.False(tracker.Add(new PointerPoint(90, 100), Start.AddMilliseconds(20)));
    }
}
