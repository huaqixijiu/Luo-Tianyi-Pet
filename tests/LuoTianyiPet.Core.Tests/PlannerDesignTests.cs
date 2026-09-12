namespace LuoTianyiPet.Core.Tests;
public class PlannerDesignTests
{
    [Theory]
    [InlineData(ReminderRepeat.Once)]
    [InlineData(ReminderRepeat.Daily)]
    [InlineData(ReminderRepeat.Weekly)]
    [InlineData(ReminderRepeat.Dates)]
    public void SkipOnlyThisOccurrence(ReminderRepeat rule)
    {
        DateTime now = new(2026, 9, 14, 13, 40, 0), due = now.Date.AddHours(14);
        ReminderItem item = new() { Calendar = true, Start = due, CheckedThrough = now, Repeat = rule, Weekdays = [DayOfWeek.Monday], Dates = [due.Date, due.Date.AddDays(7)] };
        ReminderBook book = new() { Items = [item] };
        ReminderSchedule.SkipOccurrence(item, due);
        Assert.Null(ReminderSchedule.Upcoming(item, book, now));
        Assert.False(ReminderSchedule.Advance(book, due)); Assert.Null(item.PendingAt);
        Assert.True(ReminderSchedule.OccursOn(item, book, due));
        if(rule != ReminderRepeat.Once) Assert.True(ReminderSchedule.Next(item, book, now) > due);
        item.SkippedAt = null;
        Assert.True(ReminderSchedule.Advance(book, due)); Assert.Equal(due, item.PendingAt);
    }
    [Fact]
    public void HiddenCountdownStillFiresAndRestDaysAreArbitrary()
    {
        DateTime now = new(2026,9,14,13,40,0), due=now.Date.AddHours(14);
        ReminderItem item = new() { Calendar=true, Start=due, CheckedThrough=now, HiddenCountdownAt=due };
        ReminderBook book=new() { Items=[item] };
        Assert.Null(ReminderSchedule.Upcoming(item,book,now)); Assert.True(ReminderSchedule.Advance(book,due));
        ReminderSchedule.SetRestWeekdays(book,[DayOfWeek.Monday,DayOfWeek.Tuesday],now);
        Assert.True(book.IsRest(now)); Assert.False(book.IsRest(now.AddDays(5)));
        ReminderSchedule.SetRestOverride(book,now,false,now); Assert.False(book.IsRest(now));
        ReminderSchedule.SetRestWeekdays(book,[DayOfWeek.Wednesday],now); Assert.True(book.IsRest(now.AddDays(2)));
    }
    [Fact]
    public void LunarAndLegacyContentsArePreserved()
    {
        Assert.Equal("十五",CalendarLabels.LunarDay(new(2026,9,25)));
        Assert.Equal("农历八月十五",CalendarLabels.FullLunar(new(2026,9,25)));
        ReminderItem item=new() { Title="标题",Notes="正文" };
        Assert.Equal("标题\n正文",ReminderSchedule.FullContent(item));
        item.Content="合并内容"; Assert.Equal("合并内容",ReminderSchedule.FullContent(item));
    }
}
