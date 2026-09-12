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
    public static string Get(DateTime day)
    {
        if (day < ReminderSchedule.MinimumDate || day > ReminderSchedule.MaximumDate) return "";
        List<string> labels = [];
        string? fixedHoliday = day.ToString("MM-dd") switch
        { "01-01" => "元旦", "05-01" => "劳动节", "06-01" => "儿童节", "10-01" => "国庆节", _ => null };
        if (fixedHoliday != null) labels.Add(fixedHoliday);
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
