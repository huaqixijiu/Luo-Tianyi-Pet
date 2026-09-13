namespace LuoTianyiPet.Core;

public enum ReminderPhase { Waiting, Early, EarlySnoozed, AcknowledgedEarly, Due, DueSnoozed, Done, Cancelled }
public sealed class ReminderOccurrence
{
    public Guid RuleId { get; set; }
    public DateTime At { get; set; }
    public ReminderPhase Phase { get; set; }
    public DateTime? SnoozeAt { get; set; }
    public int Revision { get; set; }
    public DateTime? RoundStartedAt { get; set; }
}
public sealed class ReminderPreferences
{
    public bool Sound { get; set; } = true;
    public bool Animation { get; set; } = false;
    public string Tone { get; set; } = "轻提示";
    public double Volume { get; set; } = 0.5;
}
public static class ReminderEngine
{
    public const int MaximumRoundSeconds = 222;
    public const int RetryMinutes = 10;
    public static string Label(ReminderItem item) => string.IsNullOrWhiteSpace(item.Title) ? item.Relative ? "倒计时" : item.Start.ToString("HH:mm") + " 提醒" : item.Title;
    public static bool Active(ReminderItem i) => i.Enabled && i.HasTime && i.PausedSeconds == null;
    public static void Initialize(ReminderBook book, DateTime now)
    {
        foreach(var i in book.Items)
        {
            i.ReminderCreated ??= !i.Calendar || i.Enabled;
            i.EarlyEnabled ??= i.Calendar && i.ShowCountdown && book.ShowUpcoming;
        }
        if(book.EngineVersion == 1) return;
        foreach(var i in book.Items)
        {
            if(i.PendingAt is DateTime due) book.Occurrences.Add(new() { RuleId=i.Id, At=due, Phase=ReminderPhase.Due });
            else if(i.SnoozeUntil is DateTime snooze) book.Occurrences.Add(new() { RuleId=i.Id, At=i.Start, Phase=ReminderPhase.DueSnoozed, SnoozeAt=snooze });
            if(i.SkippedAt is DateTime skipped) book.Occurrences.Add(new() { RuleId=i.Id, At=skipped, Phase=ReminderPhase.Cancelled });
            i.PendingAt=null; i.SnoozeUntil=null;
        }
        book.EngineVersion=1;
    }
    public static void Reconcile(ReminderBook book)
    {
        book.Occurrences.RemoveAll(o => !book.Items.Any(i => i.Id==o.RuleId && Active(i) && ReminderSchedule.OccursOn(i,book,o.At) && o.At.TimeOfDay==i.Start.TimeOfDay));
    }
    public static bool Advance(ReminderBook book, DateTime now)
    {
        bool changed=false;
        foreach(var i in book.Items.Where(Active))
        {
            // At most one catch-up instance, matching the previous application's bounded catch-up policy.
            DateTime? next=ReminderSchedule.Next(i,book,i.CheckedThrough);
            if(next is DateTime at && !book.Occurrences.Any(o=>o.RuleId==i.Id && o.At==at))
            {
                bool early=i.EarlyEnabled==true && !i.Relative && at.AddMinutes(-i.EarlyMinutes)>i.CheckedThrough;
                if(at<=now || early && at.AddMinutes(-i.EarlyMinutes)<=now)
                {
                    book.Occurrences.Add(new() { RuleId=i.Id,At=at,Phase=at<=now?ReminderPhase.Due:ReminderPhase.Early });
                    // Future instances are now tracked by their own stable occurrence record.
                    i.CheckedThrough=at<=now?now:at; changed=true;
                }
            }
        }
        foreach(var o in book.Occurrences)
        {
            if(o.Phase is ReminderPhase.Done or ReminderPhase.Cancelled) continue;
            if(o.Phase is ReminderPhase.Early or ReminderPhase.EarlySnoozed or ReminderPhase.AcknowledgedEarly && now>=o.At)
            { o.Phase=ReminderPhase.Due;o.SnoozeAt=null;o.RoundStartedAt=now;o.Revision++;changed=true; }
            else if(o.SnoozeAt is DateTime snooze && snooze<=now)
            { o.Phase=o.Phase==ReminderPhase.EarlySnoozed?ReminderPhase.Early:ReminderPhase.Due;o.SnoozeAt=null;o.RoundStartedAt=now;o.Revision++;changed=true; }
            if(o.Phase is ReminderPhase.Early or ReminderPhase.Due)
            {
                if(o.RoundStartedAt==null){o.RoundStartedAt=now;changed=true;}
                if((book.Preferences.Sound||book.Preferences.Animation) && now>=o.RoundStartedAt.Value.AddSeconds(MaximumRoundSeconds))
                { Snooze(book,o.RuleId,o.At,o.Phase,now);changed=true; }
            }
        }
        // Old terminal records are safe to prune only after the persisted schedule cursor has crossed them.
        changed |= book.Occurrences.RemoveAll(o => o.At<now.AddDays(-7) && o.Phase is ReminderPhase.Done or ReminderPhase.Cancelled && book.Items.Any(i=>i.Id==o.RuleId && i.CheckedThrough>=o.At))>0;
        return changed;
    }
    public static void Acknowledge(ReminderBook b, Guid id, DateTime at, ReminderPhase expected)
    {
        var o=b.Occurrences.FirstOrDefault(x=>x.RuleId==id && x.At==at);
        if(o==null || o.Phase!=expected) return;
        o.Phase=expected==ReminderPhase.Early?ReminderPhase.AcknowledgedEarly:ReminderPhase.Done;o.SnoozeAt=null;o.RoundStartedAt=null;
    }
    public static void Snooze(ReminderBook b,Guid id,DateTime at,ReminderPhase expected,DateTime now)
    {
        var o=b.Occurrences.FirstOrDefault(x=>x.RuleId==id && x.At==at);
        if(o==null || o.Phase!=expected) return;
        o.Phase=expected==ReminderPhase.Early?ReminderPhase.EarlySnoozed:ReminderPhase.DueSnoozed;
        o.RoundStartedAt=null;
        o.SnoozeAt=expected==ReminderPhase.Early && now.AddMinutes(10)>=at?at:now.AddMinutes(10);
    }
    public static void Cancel(ReminderBook b,Guid id,DateTime at)
    { var o=b.Occurrences.FirstOrDefault(x=>x.RuleId==id && x.At==at); if(o!=null) {o.Phase=ReminderPhase.Cancelled;o.SnoozeAt=null;} }
    public static void SetEnabled(ReminderBook b,Guid id,bool enabled,DateTime now)
    {
        var i=b.Items.FirstOrDefault(x=>x.Id==id);if(i==null)return;
        i.Enabled=enabled && i.HasTime;i.CheckedThrough=now;i.ReminderCreated=true;
        b.Occurrences.RemoveAll(o=>o.RuleId==id);i.PendingAt=null;i.SnoozeUntil=null;
    }
    public static double Remaining(ReminderItem i,DateTime now) => i.PausedSeconds ?? Math.Max(0,(i.Start-now).TotalSeconds);
    public static void Pause(ReminderBook b,Guid id,DateTime now)
    {
        var i=b.Items.First(x=>x.Id==id);if(!i.Relative || !i.Enabled || i.Start<=now)return;
        i.PausedSeconds=Remaining(i,now);b.Occurrences.RemoveAll(o=>o.RuleId==id);
    }
    public static void Resume(ReminderBook b,Guid id,DateTime now)
    {
        var i=b.Items.First(x=>x.Id==id);if(i.PausedSeconds is not double seconds)return;
        i.Start=now.AddSeconds(seconds);i.CheckedThrough=now;i.PausedSeconds=null;
    }
}
