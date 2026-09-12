using System.Globalization;
using System.Reflection;

namespace LuoTianyiPet.Core;

public static class CalendarLabels
{
    private static readonly ChineseLunisolarCalendar Lunar = new();
    private static readonly Lazy<Dictionary<string, string>> Terms = new(() =>
    {
        using Stream stream = typeof(CalendarLabels).Assembly.GetManifestResourceStream("LuoTianyiPet.Core.SolarTerms.tsv")
            ?? throw new InvalidOperationException("缺少离线节气表。");
        using StreamReader reader = new(stream);
        Dictionary<string, string> result = [];
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string[] parts = line.Split('\t');
            if (parts.Length == 2) result.Add(parts[0], parts[1]);
        }
        return result;
    });
    public static string LunarDay(DateTime day)
    {
        if (day.Date < ReminderSchedule.MinimumDate || day.Date > ReminderSchedule.MaximumDate) return "";
        int d = Lunar.GetDayOfMonth(day);
        if (d == 10) return "初十"; if (d == 20) return "二十"; if (d == 30) return "三十";
        return (d < 10 ? "初" : d < 20 ? "十" : "廿") + "一二三四五六七八九"[(d - 1) % 10];
    }
    public static string FullLunar(DateTime day)
    {
        if (day.Date < ReminderSchedule.MinimumDate || day.Date > ReminderSchedule.MaximumDate) return "";
        int m = Lunar.GetMonth(day), leap = Lunar.GetLeapMonth(Lunar.GetYear(day));
        bool isLeap = m == leap;
        if (leap > 0 && m >= leap) m--;
        return "农历" + (isLeap ? "闰" : "") + new[] { "正", "二", "三", "四", "五", "六", "七", "八", "九", "十", "冬", "腊" }[m - 1] + "月" + LunarDay(day);
    }
    public static string Get(DateTime day)
    {
        if (day < ReminderSchedule.MinimumDate || day > ReminderSchedule.MaximumDate) return "";
        List<string> labels = [];
        string? fixedHoliday = day.ToString("MM-dd") switch
        {
            "01-01" => "元旦", "02-14" => "情人节", "04-01" => "愚人节",
            "05-01" => "劳动节", "06-01" => "儿童节", "10-01" => "国庆节",
            "10-31" => "万圣夜", "11-01" => "万圣节",
            "12-24" => "平安夜", "12-25" => "圣诞节", _ => null
        };
        if (fixedHoliday != null) labels.Add(fixedHoliday);
        // Common May/June convention; Thanksgiving explicitly uses the US date.
        // These labels do not participate in work/rest-day scheduling.
        int weekOrdinal = (day.Day - 1) / 7 + 1;
        if (day.Month == 5 && day.DayOfWeek == DayOfWeek.Sunday && weekOrdinal == 2) labels.Add("母亲节");
        if (day.Month == 6 && day.DayOfWeek == DayOfWeek.Sunday && weekOrdinal == 3) labels.Add("父亲节");
        if (day.Month == 11 && day.DayOfWeek == DayOfWeek.Thursday && weekOrdinal == 4) labels.Add("感恩节（美国）");
        int year = Lunar.GetYear(day), month = Lunar.GetMonth(day), date = Lunar.GetDayOfMonth(day);
        int leap = Lunar.GetLeapMonth(year);
        bool leapMonth = leap != 0 && month == leap;
        if (leap != 0 && month >= leap) month--;
        if (!leapMonth)
        {
            string? lunarHoliday = (month, date) switch
            { (1, 1) => "春节", (1, 15) => "元宵节", (5, 5) => "端午节", (7, 7) => "七夕", (8, 15) => "中秋节", (9, 9) => "重阳节", (12, 8) => "腊八节", _ => null };
            if (lunarHoliday != null) labels.Add(lunarHoliday);
        }
        if (Lunar.GetYear(day.AddDays(1)) != year) labels.Add("除夕");
        if (Terms.Value.TryGetValue(day.ToString("yyyy-MM-dd"), out string? term)) labels.Add(term);
        return string.Join(" · ", labels);
    }
}
