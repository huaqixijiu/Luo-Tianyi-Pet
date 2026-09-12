namespace LuoTianyiPet.Core.Tests;

public class ReminderTests
{
    private static DateTime At(int day, int hour = 9) => new(2026, 9, day, hour, 0, 0);
    [Fact]
    public void RestOverrideAffectsWorkdayButNotWeekdayRepeat()
    {
        ReminderBook b = new(); b.RestOverrides["2026-09-14"] = true; b.RestOverrides["2026-09-19"] = false;
        ReminderItem item = new() { Start = At(14), Repeat = ReminderRepeat.Workdays };
        Assert.False(ReminderSchedule.OccursOn(item, b, At(14)));
        Assert.True(ReminderSchedule.OccursOn(item, b, At(19)));
        item.Repeat = ReminderRepeat.Weekly; item.Weekdays = [DayOfWeek.Monday];
        Assert.True(ReminderSchedule.OccursOn(item, b, At(14)));
        b.RestWeekdays = [DayOfWeek.Sunday];
        Assert.True(b.IsRest(At(14)));
    }
    [Fact]
    public void SleepCollapsesMissedOccurrencesAndDismissDoesNotReplay()
    {
        ReminderItem i = new() { Start = At(13), CheckedThrough = At(12), Repeat = ReminderRepeat.Daily };
        ReminderBook b = new() { Items = [i] };
        Assert.True(ReminderSchedule.Advance(b, At(20, 11)));
        Assert.Equal(At(13), i.PendingAt);
        Assert.False(ReminderSchedule.Advance(b, At(20, 12)));
        ReminderSchedule.Dismiss(i, At(20, 12));
        Assert.False(ReminderSchedule.Advance(b, At(20, 13)));
        Assert.Equal(At(21), ReminderSchedule.Next(i, b, At(20, 12)));
    }
    [Fact]
    public void SnoozeRearmsExactlyOnceAndClockRollbackDoesNotReplay()
    {
        ReminderItem i = new() { Start = At(13), CheckedThrough = At(13, 10), SnoozeUntil = At(13, 11) };
        ReminderBook b = new() { Items = [i] };
        Assert.False(ReminderSchedule.Advance(b, At(13, 10)));
        Assert.True(ReminderSchedule.Advance(b, At(13, 11)));
        ReminderSchedule.Dismiss(i, At(13, 12));
        Assert.False(ReminderSchedule.Advance(b, At(13, 9)));
        Assert.False(i.Enabled);
    }
    [Fact]
    public void MultipleDatesExpireWithoutRemovingCalendarHistory()
    {
        ReminderBook b = new();
        ReminderItem i = new() { Calendar = true, Start = At(14), Repeat = ReminderRepeat.Dates, Dates = [At(14), At(18), At(25)] };
        Assert.Equal(At(18), ReminderSchedule.Next(i, b, At(15)));
        Assert.Null(ReminderSchedule.Next(i, b, At(26)));
        Assert.True(ReminderSchedule.OccursOn(i, b, At(14)));
        i.Enabled = false; Assert.Null(ReminderSchedule.Next(i, b, At(15)));
    }
    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(86400, true)] [InlineData(86401, false)]
    public void CountdownLimit(int seconds, bool valid)
    {
        ReminderBook b = new() { Items = [new() { Title = "计时", Start = At(14), Relative = true, DurationSeconds = seconds }] };
        if (valid) ReminderSchedule.Validate(b); else Assert.Throws<ArgumentException>(() => ReminderSchedule.Validate(b));
    }
    [Theory]
    [InlineData(2026, 2, 17, "春节")] [InlineData(2026, 2, 16, "除夕")]
    [InlineData(2026, 6, 19, "端午节")] [InlineData(2026, 9, 25, "中秋节")]
    [InlineData(2026, 4, 5, "清明")] [InlineData(2099, 1, 1, "元旦")]
    public void OfflineLabels(int y, int m, int d, string expected) => Assert.Contains(expected, CalendarLabels.Get(new(y, m, d)));
    [Fact]
    public void OfflineEveryDayThrough2099IsSupported()
    {
        for (DateTime d = ReminderSchedule.MinimumDate; d <= ReminderSchedule.MaximumDate; d = d.AddDays(1))
            Assert.NotNull(CalendarLabels.Get(d));
    }
    [Fact]
    public void NoRestWeekdaysStillHonorsDateException()
    {
        ReminderBook b = new() { RestWeekdays = [] }; b.RestOverrides["2026-09-25"] = true;
        ReminderItem i = new() { Start = At(13), Repeat = ReminderRepeat.RestDays };
        Assert.Equal(At(25), ReminderSchedule.Next(i, b, At(13)));
        b.RestOverrides.Clear(); Assert.Null(ReminderSchedule.Next(i, b, At(13)));
    }
    [Fact]
    public void WorkdayChangesDoNotRetroactivelyRingOrAlterFixedAlarms()
    {
        ReminderItem work = new() { Start = At(13), Repeat = ReminderRepeat.Workdays, CheckedThrough = At(12) };
        ReminderItem fixedItem = new() { Start = At(14), CheckedThrough = At(12) };
        ReminderBook b = new() { Items = [work, fixedItem] };
        ReminderSchedule.SetRestOverride(b, At(13), false, At(13, 15));
        Assert.False(ReminderSchedule.Advance(b, At(13, 16)));
        Assert.Equal(At(12), fixedItem.CheckedThrough);
        work.PendingAt = At(14);
        ReminderSchedule.SetRestOverride(b, At(14), true, At(14, 15));
        Assert.Null(work.PendingAt);
    }
}
