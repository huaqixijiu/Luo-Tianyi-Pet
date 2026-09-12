namespace LuoTianyiPet.Core.Tests;

public class InternationalHolidayTests
{
    [Theory]
    [InlineData(2026, 2, 14, "情人节")]
    [InlineData(2026, 4, 1, "愚人节")]
    [InlineData(2026, 10, 31, "万圣夜")]
    [InlineData(2026, 11, 1, "万圣节")]
    [InlineData(2026, 12, 24, "平安夜")]
    [InlineData(2026, 12, 25, "圣诞节")]
    [InlineData(2026, 5, 10, "母亲节")]
    [InlineData(2026, 6, 21, "父亲节")]
    [InlineData(2026, 11, 26, "感恩节（美国）")]
    [InlineData(2099, 12, 25, "圣诞节")]
    public void KnownDates(int year, int month, int day, string label)
        => Assert.Contains(label, CalendarLabels.Get(new DateTime(year, month, day)));

    [Fact]
    public void EverySupportedYearHasExactlyOneFloatingHoliday()
    {
        for (int year = 2026; year <= 2099; year++)
        foreach (var item in new[] { (5, 8, 14, "母亲节", DayOfWeek.Sunday), (6, 15, 21, "父亲节", DayOfWeek.Sunday), (11, 22, 28, "感恩节（美国）", DayOfWeek.Thursday) })
        {
            var dates = Enumerable.Range(1, DateTime.DaysInMonth(year, item.Item1))
                .Select(day => new DateTime(year, item.Item1, day))
                .Where(day => CalendarLabels.Get(day).Contains(item.Item4)).ToArray();
            DateTime date = Assert.Single(dates);
            Assert.InRange(date.Day, item.Item2, item.Item3);
            Assert.Equal(item.Item5, date.DayOfWeek);
        }
    }
    [Fact]
    public void SharedDatePreservesSolarTermAndWorkdayPreference()
    {
        DateTime day = new(2026, 6, 21);
        Assert.Contains("父亲节", CalendarLabels.Get(day));
        Assert.Contains("夏至", CalendarLabels.Get(day));
        ReminderBook book = new(); book.RestOverrides["2026-06-21"] = false;
        string before = CalendarLabels.Get(day);
        Assert.False(book.IsRest(day));
        Assert.Equal(before, CalendarLabels.Get(day));
        Assert.Empty(book.Items);
    }
}
