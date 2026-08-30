using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class BirthdayEasterEggSchedulerTests
{
    [Theory]
    [InlineData(2026, 7, 12, true)]
    [InlineData(2026, 12, 12, true)]
    [InlineData(2026, 7, 13, false)]
    public void RecognizesConfiguredBirthdays(int year, int month, int day, bool expected)
    {
        DateTimeOffset now = new(year, month, day, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.Equal(expected, BirthdayEasterEggScheduler.IsBirthday(now));
    }

    [Fact]
    public void TriggersOnlyAfterRandomWindowWhileIdle()
    {
        DateTimeOffset now = new(2026, 12, 12, 12, 0, 0, TimeSpan.FromHours(8));
        BirthdayEasterEggScheduler scheduler = new(_ => 45);

        Assert.False(scheduler.ShouldTrigger(now, idleEligible: true));
        Assert.False(scheduler.ShouldTrigger(now.AddMinutes(45), idleEligible: false));
        Assert.True(scheduler.ShouldTrigger(now.AddMinutes(45), idleEligible: true));
        Assert.False(scheduler.ShouldTrigger(now.AddMinutes(46), idleEligible: true));
    }
}
