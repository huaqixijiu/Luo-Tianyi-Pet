namespace LuoTianyiPet.Core.Tests;
public class UpcomingReminderTests
{
    [Theory]
    [InlineData(1801, false)]
    [InlineData(1800, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void CountdownBoundary(int seconds, bool expected)
    {
        DateTime now = new(2026, 9, 13, 12, 0, 0);
        ReminderItem item = new() { Calendar = true, Start = now.AddSeconds(seconds) };
        Assert.Equal(expected, ReminderSchedule.Upcoming(item, new(), now) != null);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisablingCountdownPreservesDueReminder(bool global)
    {
        DateTime now = new(2026, 9, 13, 12, 0, 0);
        ReminderItem item = new() { Calendar = true, Start = now.AddMinutes(20), CheckedThrough = now };
        ReminderBook book = new() { Items = [item] };
        if (global) book.ShowUpcoming = false; else item.ShowCountdown = false;
        Assert.Null(ReminderSchedule.Upcoming(item, book, now));
        Assert.Equal(item.Start, ReminderSchedule.Next(item, book, now));
        Assert.True(ReminderSchedule.Advance(book, item.Start));
        Assert.Equal(item.Start, item.PendingAt);
    }
    [Fact]
    public void DisabledAndPendingDoNotCountDownAndRelativeRangeIsPreserved()
    {
        DateTime now = new(2026, 9, 13, 12, 0, 0);
        ReminderBook book = new();
        ReminderItem item = new() { Calendar = true, Start = now.AddMinutes(20), Enabled = false };
        Assert.Null(ReminderSchedule.Upcoming(item, book, now));
        item.Enabled = true; item.PendingAt = now;
        Assert.Null(ReminderSchedule.Upcoming(item, book, now));
        item.PendingAt = null; item.Calendar = false; item.Relative = true; item.Start = now.AddHours(24);
        Assert.Equal(item.Start, ReminderSchedule.Upcoming(item, book, now));
        book.ShowRemaining = false;
        Assert.Null(ReminderSchedule.Upcoming(item, book, now));
    }
}
