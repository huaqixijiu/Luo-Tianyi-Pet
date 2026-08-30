using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class GenshinIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void PresenceTrackerTriggersOnceForMultipleTargetProcesses()
    {
        ProtectedGamePresenceTracker tracker = new(["YuanShen.exe", "GenshinImpact.exe"]);

        Assert.Equal(
            ProtectedGamePresenceTransition.BecameRunning,
            tracker.ObserveStarted("YuanShen", 10));
        Assert.Equal(
            ProtectedGamePresenceTransition.None,
            tracker.ObserveStarted("GenshinImpact.exe", 11));
        Assert.Equal(
            ProtectedGamePresenceTransition.None,
            tracker.ObserveStopped("YuanShen.exe", 10));
        Assert.Equal(
            ProtectedGamePresenceTransition.BecameStopped,
            tracker.ObserveStopped("GenshinImpact", 11));
    }

    [Fact]
    public void SeededProcessDoesNotCreateALaunchTransition()
    {
        ProtectedGamePresenceTracker tracker = new(["YuanShen.exe"]);

        tracker.Seed("YuanShen.exe", 10);

        Assert.True(tracker.IsRunning);
        Assert.Equal(
            ProtectedGamePresenceTransition.None,
            tracker.ObserveStarted("YuanShen.exe", 10));
    }

    [Fact]
    public void PresenceTrackerIgnoresUnlistedProcesses()
    {
        ProtectedGamePresenceTracker tracker = new(["YuanShen.exe"]);

        Assert.Equal(
            ProtectedGamePresenceTransition.None,
            tracker.ObserveStarted("notepad.exe", 20));
        Assert.False(tracker.IsRunning);
        Assert.False(tracker.IsTargetProcess(null));
    }

    [Fact]
    public void CameoTriggersAtRandomOffsetAndSchedulesNextHour()
    {
        Queue<int> values = new([10, 5, 5]);
        GenshinBackgroundCameoScheduler scheduler = new((_, _) => values.Dequeue());

        Assert.Equal(GenshinCameoScheduleDecision.None, scheduler.Update(Now, true, true));
        Assert.Equal(GenshinCameoScheduleDecision.None, scheduler.Update(Now.AddMinutes(9), true, true));
        Assert.Equal(GenshinCameoScheduleDecision.Trigger, scheduler.Update(Now.AddMinutes(10), true, true));
        Assert.Equal(GenshinCameoScheduleDecision.None, scheduler.Update(Now.AddMinutes(64), true, true));
        Assert.Equal(GenshinCameoScheduleDecision.Trigger, scheduler.Update(Now.AddMinutes(65), true, true));
    }

    [Fact]
    public void CameoIsPostponedAfterGameReturnsToBackground()
    {
        Queue<int> values = new([10, 7, 5]);
        GenshinBackgroundCameoScheduler scheduler = new((_, _) => values.Dequeue());
        scheduler.Update(Now, true, true);

        scheduler.Update(Now.AddMinutes(9), true, false);
        Assert.Equal(GenshinCameoScheduleDecision.None, scheduler.Update(Now.AddMinutes(20), true, true));
        Assert.Equal(GenshinCameoScheduleDecision.None, scheduler.Update(Now.AddMinutes(26), true, true));
        Assert.Equal(GenshinCameoScheduleDecision.Trigger, scheduler.Update(Now.AddMinutes(27), true, true));
    }

    [Fact]
    public void CameoResetOnProcessExitStartsANewWindow()
    {
        Queue<int> values = new([5, 55, 5]);
        GenshinBackgroundCameoScheduler scheduler = new((_, _) => values.Dequeue());
        scheduler.Update(Now, true, true);
        scheduler.Update(Now.AddMinutes(1), false, false);

        scheduler.Update(Now.AddMinutes(2), true, true);

        Assert.Equal(GenshinCameoScheduleDecision.None, scheduler.Update(Now.AddMinutes(56), true, true));
        Assert.Equal(GenshinCameoScheduleDecision.Trigger, scheduler.Update(Now.AddMinutes(57), true, true));
    }

    [Theory]
    [InlineData(0, 24, 24)]
    [InlineData(0.5, 374, 274)]
    [InlineData(1, 724, 524)]
    public void RandomPositionStaysInsideSafeWorkArea(double unit, double expectedX, double expectedY)
    {
        RandomPetPositionSelector selector = new(() => unit);

        PointerPoint point = selector.Select(
            new DesktopRectangle(0, 0, 1000, 800),
            petWidth: 252,
            petHeight: 252,
            safeMargin: 24);

        Assert.Equal(expectedX, point.X);
        Assert.Equal(expectedY, point.Y);
    }
}
