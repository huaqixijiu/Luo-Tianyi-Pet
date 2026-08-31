using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class DownwardFlingTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FastLongDownwardReleaseRequestsLanding()
    {
        DownwardFlingTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 100), Start);
        tracker.Add(new PointerPoint(103, 135), Start.AddMilliseconds(80));

        bool result = tracker.Complete(
            new PointerPoint(106, 205),
            Start.AddMilliseconds(150));

        Assert.True(result);
    }

    [Fact]
    public void SlowDownwardMoveDoesNotRequestLanding()
    {
        DownwardFlingTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 100), Start);
        tracker.Add(new PointerPoint(100, 160), Start.AddMilliseconds(500));

        bool result = tracker.Complete(
            new PointerPoint(100, 205),
            Start.AddMilliseconds(900));

        Assert.False(result);
    }

    [Fact]
    public void ShortFastMoveDoesNotRequestLanding()
    {
        DownwardFlingTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 100), Start);

        bool result = tracker.Complete(
            new PointerPoint(100, 150),
            Start.AddMilliseconds(50));

        Assert.False(result);
    }

    [Fact]
    public void UpwardReleaseDoesNotRequestLanding()
    {
        DownwardFlingTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 200), Start);

        bool result = tracker.Complete(
            new PointerPoint(100, 100),
            Start.AddMilliseconds(100));

        Assert.False(result);
    }

    [Fact]
    public void SlightlyEarlierMoveTimestampIsClampedInsteadOfThrowing()
    {
        DownwardFlingTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 100), Start.AddMilliseconds(1));

        tracker.Add(new PointerPoint(100, 130), Start);
        bool result = tracker.Complete(
            new PointerPoint(100, 205),
            Start.AddMilliseconds(151));

        Assert.True(result);
    }

    [Fact]
    public void EarlierReleaseTimestampFailsClosedInsteadOfThrowing()
    {
        DownwardFlingTracker tracker = new();
        tracker.Begin(new PointerPoint(100, 100), Start);
        tracker.Add(new PointerPoint(100, 180), Start.AddMilliseconds(100));

        bool result = tracker.Complete(
            new PointerPoint(100, 205),
            Start.AddMilliseconds(90));

        Assert.False(result);
    }
}
