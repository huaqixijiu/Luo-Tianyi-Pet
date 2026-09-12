namespace LuoTianyiPet.Core;

public enum ReminderRepeat { Once, Daily, Weekly, Dates, Workdays, RestDays }

public sealed class ReminderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string? Content { get; set; }
    public DateTime? SkippedAt { get; set; }
    public DateTime? HiddenCountdownAt { get; set; }
    public bool Calendar { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Sound { get; set; } = true;
    public bool ShowCountdown { get; set; } = true;
    public bool Relative { get; set; }
    public DateTime Start { get; set; } = DateTime.Now;
    public int DurationSeconds { get; set; } = 1500;
    public ReminderRepeat Repeat { get; set; }
    public List<DayOfWeek> Weekdays { get; set; } = [];
    public List<DateTime> Dates { get; set; } = [];
    public DateTime CheckedThrough { get; set; } = DateTime.Now;
    public DateTime? PendingAt { get; set; }
    public DateTime? SnoozeUntil { get; set; }
}

public sealed class ReminderBook
{
    public int Version { get; set; } = 1;
    public List<ReminderItem> Items { get; set; } = [];
    public List<DayOfWeek> RestWeekdays { get; set; } = [DayOfWeek.Saturday, DayOfWeek.Sunday];
    public Dictionary<string, bool> RestOverrides { get; set; } = [];
    public int LastDurationSeconds { get; set; } = 1500;
    public bool WeekView { get; set; }
    public bool ShowRemaining { get; set; } = true;
    public bool ShowUpcoming { get; set; } = true;
    public bool IsRest(DateTime day) => RestOverrides.TryGetValue(day.ToString("yyyy-MM-dd"), out bool rest)
        ? rest : RestWeekdays.Contains(day.DayOfWeek);
}

public static class ReminderSchedule
{
    public static readonly DateTime MinimumDate = new(2026, 1, 1);
    public static readonly DateTime MaximumDate = new(2099, 12, 31);

    public static string FullContent(ReminderItem item) => item.Content ?? (item.Title + (item.Notes.Length == 0 ? "" : "\n" + item.Notes));

    public static void SkipOccurrence(ReminderItem item, DateTime at)
    { item.SkippedAt = at; if (item.PendingAt == at) item.PendingAt = null; if (item.SnoozeUntil == at) item.SnoozeUntil = null; }

    public static DateTime? Upcoming(ReminderItem item, ReminderBook book, DateTime now)
    {
        if (!item.Enabled || item.PendingAt != null) return null;
        if (item.Calendar ? !book.ShowUpcoming || !item.ShowCountdown : !item.Relative || !book.ShowRemaining) return null;
        DateTime? at = Next(item, book, now);
        return at != null && at != item.HiddenCountdownAt && (!item.Calendar || at.Value - now <= TimeSpan.FromMinutes(30)) ? at : null;
    }

    public static bool OccursOn(ReminderItem item, ReminderBook book, DateTime day)
    {
        if (day.Date < item.Start.Date || day.Date > MaximumDate) return false;
        return item.Repeat switch
        {
            ReminderRepeat.Once => day.Date == item.Start.Date,
            ReminderRepeat.Daily => true,
            ReminderRepeat.Weekly => item.Weekdays.Contains(day.DayOfWeek),
            ReminderRepeat.Dates => item.Dates.Any(d => d.Date == day.Date),
            ReminderRepeat.Workdays => !book.IsRest(day),
            ReminderRepeat.RestDays => book.IsRest(day),
            _ => false,
        };
    }

    public static DateTime? Next(ReminderItem item, ReminderBook book, DateTime after)
    {
        DateTime? at = NextUnfiltered(item, book, after);
        return at != null && at == item.SkippedAt ? NextUnfiltered(item, book, at.Value) : at;
    }
    private static DateTime? NextUnfiltered(ReminderItem item, ReminderBook book, DateTime after)
    {
        if (!item.Enabled) return null;
        if (item.SnoozeUntil is DateTime snooze && snooze > after) return snooze;
        if (item.Repeat == ReminderRepeat.Once) return item.Start > after ? item.Start : null;
        if (item.Repeat == ReminderRepeat.Dates)
            return item.Dates.Select(d => d.Date + item.Start.TimeOfDay).Where(d => d > after && d >= item.Start)
                .Select(d => (DateTime?)d).OrderBy(d => d).FirstOrDefault();
        if (item.Repeat == ReminderRepeat.Weekly && item.Weekdays.Count == 0) return null;
        if ((item.Repeat == ReminderRepeat.Workdays && book.RestWeekdays.Distinct().Count() == 7) ||
            (item.Repeat == ReminderRepeat.RestDays && book.RestWeekdays.Count == 0))
            return book.RestOverrides.Where(p => p.Value == (item.Repeat == ReminderRepeat.RestDays))
                .Select(p => DateTime.ParseExact(p.Key, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + item.Start.TimeOfDay)
                .Where(d => d > after && d >= item.Start).Select(d => (DateTime?)d).OrderBy(d => d).FirstOrDefault();
        DateTime first = after.Date > item.Start.Date ? after.Date : item.Start.Date;
        // A week finds ordinary repeats; overrides can extend the search but never past the supported calendar.
        for (DateTime day = first; day <= MaximumDate; day = day.AddDays(1))
        {
            DateTime at = day + item.Start.TimeOfDay;
            if (at > after && OccursOn(item, book, day)) return at;
        }
        return null;
    }

    // One persistent pending entry per item, even after a long sleep or process restart.
    public static bool Advance(ReminderBook book, DateTime now)
    {
        bool changed = false;
        foreach (ReminderItem item in book.Items.Where(i => i.Enabled))
        {
            if (item.SnoozeUntil is DateTime snooze)
            {
                if (snooze > now) continue;
                item.SnoozeUntil = null;
                item.PendingAt = snooze;
                item.CheckedThrough = now;
                changed = true;
                continue;
            }
            if (item.PendingAt != null) continue;
            DateTime? next = Next(item, book, item.CheckedThrough);
            if (next is DateTime due && due <= now)
            {
                item.PendingAt = due;
                item.CheckedThrough = now;
                changed = true;
            }
        }
        return changed;
    }

    public static void Dismiss(ReminderItem item, DateTime now)
    {
        item.PendingAt = null;
        item.SnoozeUntil = null;
        item.CheckedThrough = now > item.CheckedThrough ? now : item.CheckedThrough;
        if (item.Repeat == ReminderRepeat.Once) item.Enabled = false;
    }

    public static void SetRestOverride(ReminderBook book, DateTime day, bool? rest, DateTime now)
    {
        string key = day.ToString("yyyy-MM-dd");
        if (rest == null) book.RestOverrides.Remove(key); else book.RestOverrides[key] = rest.Value;
        RebaseWorkdayReminders(book, now);
    }
    public static void SetRestWeekdays(ReminderBook book, List<DayOfWeek> days, DateTime now)
    { book.RestWeekdays = days; RebaseWorkdayReminders(book, now); }
    private static void RebaseWorkdayReminders(ReminderBook book, DateTime now)
    {
        foreach (var item in book.Items.Where(i => i.Repeat is ReminderRepeat.Workdays or ReminderRepeat.RestDays))
        {
            if (now > item.CheckedThrough) item.CheckedThrough = now;
            if (item.PendingAt is DateTime pending && !OccursOn(item, book, pending))
            { item.PendingAt = null; item.SnoozeUntil = null; }
        }
    }

    public static void Validate(ReminderBook book)
    {
        if (book.Version != 1 || book.Items == null || book.RestWeekdays == null || book.RestOverrides == null)
            throw new ArgumentException("不支持的数据格式。");
        if (book.Items.Count > 10000 || book.Items.Any(i => i is null) || book.Items.Select(i => i.Id).Distinct().Count() != book.Items.Count)
            throw new ArgumentException("记录过多或编号重复。");
        if (book.LastDurationSeconds < 1 || book.LastDurationSeconds > 86400 ||
            book.RestWeekdays.Any(d => (int)d < 0 || (int)d > 6)) throw new ArgumentException("作息或时长无效。");
        foreach (var pair in book.RestOverrides)
            if (!DateTime.TryParseExact(pair.Key, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime day) || day < MinimumDate || day > MaximumDate)
                throw new ArgumentException("调整日期超出范围。");
        foreach (ReminderItem item in book.Items)
        {
            if (item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 120 ||
                (item.Content != null && item.Content.Length > 10122) || item.Notes == null || item.Notes.Length > 10000 || item.Start.Date < MinimumDate || item.Start.Date > MaximumDate ||
                item.Weekdays == null || item.Dates == null || !Enum.IsDefined(typeof(ReminderRepeat), item.Repeat) ||
                item.Weekdays.Any(d => (int)d < 0 || (int)d > 6) ||
                item.Dates.Any(d => d.Date < MinimumDate || d.Date > MaximumDate) ||
                (item.Repeat == ReminderRepeat.Weekly && item.Weekdays.Count == 0) ||
                (item.Repeat == ReminderRepeat.Dates && item.Dates.Count == 0) ||
                (item.Relative && (item.DurationSeconds < 1 || item.DurationSeconds > 86400 || item.Repeat != ReminderRepeat.Once)))
                throw new ArgumentException("请检查标题、日期、重复星期和时长（最多 24 小时）。");
        }
    }
}
