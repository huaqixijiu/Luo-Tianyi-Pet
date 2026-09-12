namespace LuoTianyiPet.Core.Tests;
public class PlannerGroupTests
{
    [Fact]
    public void DeleteOneDatePreservesOthersAndGroupDeletionPreservesUnrelated()
    {
        DateTime first = new(2026,9,15,14,0,0);
        ReminderItem group = new() { Calendar=true, Repeat=ReminderRepeat.Dates, Start=first, Dates=[first.Date,first.AddDays(1).Date,first.AddDays(2).Date], PendingAt=first };
        ReminderItem other = new() { Start=first };
        ReminderBook book = new() { Items=[group,other] };
        ReminderSchedule.DeleteDate(book,group.Id,first);
        Assert.Equal(2,group.Dates.Count); Assert.Null(group.PendingAt);
        Assert.False(ReminderSchedule.OccursOn(group,book,first)); Assert.Equal(first.AddDays(1),ReminderSchedule.Next(group,book,first.AddHours(-1)));
        Assert.Equal(1,ReminderSchedule.DeleteGroups(book,[group.Id])); Assert.Same(other,Assert.Single(book.Items));
    }
    [Theory]
    [InlineData(ReminderRepeat.Daily)]
    [InlineData(ReminderRepeat.Weekly)]
    [InlineData(ReminderRepeat.Workdays)]
    public void RepeatingGroupCanExcludeOneDayWithoutCancellingFuture(ReminderRepeat rule)
    {
        DateTime day = new(2026,9,14,9,0,0);
        ReminderItem item=new() { Start=day, Repeat=rule, Weekdays=[DayOfWeek.Monday] };
        ReminderBook book=new() {Items=[item]};
        ReminderSchedule.DeleteDate(book,item.Id,day);
        Assert.False(ReminderSchedule.OccursOn(item,book,day)); Assert.True(ReminderSchedule.Next(item,book,day.AddMinutes(-1))>day);
    }
    [Fact]
    public void RemovingFinalDateRemovesGroupAndBulkDeleteIsScoped()
    {
        DateTime day=new(2026,9,15);
        ReminderItem item=new() {Start=day,Repeat=ReminderRepeat.Dates,Dates=[day]};
        ReminderBook book=new() {Items=[item]}; ReminderSchedule.DeleteDate(book,item.Id,day); Assert.Empty(book.Items);
        book.Items=[new(),new(),new()]; var survivor=book.Items[2];
        Assert.Equal(2,ReminderSchedule.DeleteGroups(book,book.Items.Take(2).Select(i=>i.Id).ToArray())); Assert.Same(survivor,Assert.Single(book.Items));
    }
}
