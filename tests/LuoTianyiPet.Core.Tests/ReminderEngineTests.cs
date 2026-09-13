using LuoTianyiPet.Core;
namespace LuoTianyiPet.Core.Tests;
public sealed class ReminderEngineTests
{
    private static readonly DateTime Day=new(2026,9,16);
    private static (ReminderBook,ReminderItem) Setup()
    {
        ReminderItem i=new(){Title="会议",Calendar=true,Start=Day.AddHours(14),CheckedThrough=Day.AddHours(12),EarlyEnabled=true,ReminderCreated=true,Repeat=ReminderRepeat.Daily};
        ReminderBook b=new(){EngineVersion=1,Items=[i]};return(b,i);
    }
    [Fact]public void EarlyAcknowledgeKeepsDueAndNextDay()
    {
        var(b,i)=Setup();Assert.False(ReminderEngine.Advance(b,Day.AddHours(13)));Assert.True(ReminderEngine.Advance(b,Day.AddHours(13.5)));
        var o=Assert.Single(b.Occurrences);Assert.Equal(ReminderPhase.Early,o.Phase);
        ReminderEngine.Acknowledge(b,i.Id,o.At,ReminderPhase.Early);Assert.Equal(ReminderPhase.AcknowledgedEarly,o.Phase);
        ReminderEngine.Advance(b,o.At);Assert.Equal(ReminderPhase.Due,o.Phase);ReminderEngine.Acknowledge(b,i.Id,o.At,ReminderPhase.Due);
        ReminderEngine.Advance(b,Day.AddDays(1).AddHours(13.5));Assert.Equal(2,b.Occurrences.Count);Assert.True(i.Enabled);
    }
    [Fact]public void SkipIsPerOccurrenceAndDoesNotDisableRule()
    {
        var(b,i)=Setup();ReminderEngine.Advance(b,Day.AddHours(13.5));ReminderEngine.Cancel(b,i.Id,i.Start);ReminderEngine.Advance(b,i.Start);
        Assert.Equal(ReminderPhase.Cancelled,b.Occurrences[0].Phase);Assert.True(i.Enabled);
        ReminderEngine.Advance(b,Day.AddDays(1).AddHours(13.5));Assert.Equal(ReminderPhase.Early,b.Occurrences[1].Phase);
    }
    [Theory][InlineData(13,35,false)][InlineData(13,55,true)]public void EarlySnoozeTransitionsAtCorrectStage(int hour,int minute,bool reachesDue)
    {
        var(b,i)=Setup();var now=Day.AddHours(hour).AddMinutes(minute);ReminderEngine.Advance(b,now);ReminderEngine.Snooze(b,i.Id,i.Start,ReminderPhase.Early,now);
        Assert.Equal(ReminderPhase.EarlySnoozed,b.Occurrences[0].Phase);ReminderEngine.Advance(b,now.AddMinutes(10));
        Assert.Equal(reachesDue?ReminderPhase.Due:ReminderPhase.Early,b.Occurrences[0].Phase);
    }
    [Fact]public void DueSnoozeDoesNotRestartCountdownAndDuplicateClickIsIgnored()
    {
        var(b,i)=Setup();i.Relative=true;i.Repeat=ReminderRepeat.Once;i.DurationSeconds=1500;ReminderEngine.Advance(b,i.Start);
        ReminderEngine.Snooze(b,i.Id,i.Start,ReminderPhase.Due,i.Start);var snooze=b.Occurrences[0].SnoozeAt;
        ReminderEngine.Snooze(b,i.Id,i.Start,ReminderPhase.Due,i.Start.AddMinutes(1));Assert.Equal(snooze,b.Occurrences[0].SnoozeAt);
        ReminderEngine.Advance(b,i.Start.AddMinutes(10));Assert.Equal(ReminderPhase.Due,b.Occurrences[0].Phase);Assert.Equal(Day.AddHours(14),i.Start);
    }
    [Fact]public void MultipleRulesAndMultipleDatesRemainIndependent()
    {
        var(b,i)=Setup();i.Repeat=ReminderRepeat.Dates;i.Dates=[Day,Day.AddDays(1)];var other=new ReminderItem{Start=i.Start,Title="other",CheckedThrough=i.CheckedThrough,EarlyEnabled=true};b.Items.Add(other);
        ReminderEngine.Advance(b,Day.AddHours(13.5));Assert.Equal(2,b.Occurrences.Count);ReminderEngine.Cancel(b,i.Id,i.Start);Assert.Equal(ReminderPhase.Early,b.Occurrences.Single(o=>o.RuleId==other.Id).Phase);
        ReminderEngine.Advance(b,Day.AddDays(1).AddHours(13.5));Assert.Contains(b.Occurrences,o=>o.RuleId==i.Id&&o.At==i.Start.AddDays(1)&&o.Phase==ReminderPhase.Early);
    }
    [Fact]public void CreatedAfterEarlyTimeSkipsEarlyWithoutCapsule()
    {
        var(b,i)=Setup();i.CheckedThrough=Day.AddHours(13.75);Assert.False(ReminderEngine.Advance(b,Day.AddHours(13.9)));Assert.Empty(b.Occurrences);ReminderEngine.Advance(b,i.Start);Assert.Equal(ReminderPhase.Due,Assert.Single(b.Occurrences).Phase);
    }
    [Fact]public void DisableAndDeleteClearInstanceButKeepOtherRule()
    {
        var(b,i)=Setup();ReminderEngine.Advance(b,Day.AddHours(13.5));ReminderEngine.SetEnabled(b,i.Id,false,Day.AddHours(13.6));Assert.Empty(b.Occurrences);Assert.Single(b.Items);
        ReminderEngine.SetEnabled(b,i.Id,true,Day.AddHours(13.7));Assert.False(ReminderEngine.Advance(b,Day.AddHours(13.8)));b.Items.Clear();ReminderEngine.Reconcile(b);Assert.Empty(b.Occurrences);
    }
    [Fact]public void TimeEditAndDateDeleteRemoveStaleInstances()
    {
        var(b,i)=Setup();ReminderEngine.Advance(b,Day.AddHours(13.5));i.Start=i.Start.AddHours(1);ReminderEngine.Reconcile(b);Assert.Empty(b.Occurrences);
        i.CheckedThrough=Day.AddHours(14);ReminderEngine.Advance(b,Day.AddHours(14.5));Assert.Single(b.Occurrences);ReminderSchedule.DeleteDate(b,i.Id,Day);ReminderEngine.Reconcile(b);Assert.Empty(b.Occurrences);Assert.Single(b.Items);
    }
    [Theory][InlineData(ReminderRepeat.Workdays,true)][InlineData(ReminderRepeat.Weekly,false)][InlineData(ReminderRepeat.RestDays,false)]
    public void SaturdayWorkOverrideDoesNotChangeWeeklyRule(ReminderRepeat rule,bool saturday)
    {
        ReminderBook b=new();var sat=new DateTime(2026,9,19);ReminderSchedule.SetRestOverride(b,sat,false,Day);ReminderItem i=new(){Title="test",Start=Day,Repeat=rule,Weekdays=[DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday]};Assert.Equal(saturday,ReminderSchedule.OccursOn(i,b,sat));
    }
    [Theory][InlineData(ReminderRepeat.Workdays,false)][InlineData(ReminderRepeat.Weekly,true)][InlineData(ReminderRepeat.RestDays,true)]
    public void WednesdayRestOverrideDoesNotChangeWeeklyRule(ReminderRepeat rule,bool wednesday)
    {
        ReminderBook b=new();ReminderSchedule.SetRestOverride(b,Day,true,Day.AddDays(-1));ReminderItem i=new(){Title="test",Start=Day,Repeat=rule,Weekdays=[DayOfWeek.Wednesday]};Assert.Equal(wednesday,ReminderSchedule.OccursOn(i,b,Day));
    }
    [Fact]public void PauseUsesDeadlineAndResumeUsesFrozenRemainder()
    {
        var(b,i)=Setup();i.Relative=true;i.Repeat=ReminderRepeat.Once;i.EarlyEnabled=false;var time=i.Start.AddSeconds(-75);ReminderEngine.Pause(b,i.Id,time);Assert.Equal(75,i.PausedSeconds);ReminderEngine.Advance(b,time.AddHours(1));Assert.Empty(b.Occurrences);
        ReminderEngine.Resume(b,i.Id,time.AddHours(1));Assert.Equal(time.AddHours(1).AddSeconds(75),i.Start);Assert.Null(i.PausedSeconds);
    }
    [Fact]public void UntimedNeverSchedulesAndOptionalAlarmTitleValidates()
    {
        var(b,i)=Setup();i.HasTime=false;Assert.False(ReminderEngine.Advance(b,i.Start.AddDays(3)));Assert.Null(ReminderSchedule.Next(i,b,Day));i.HasTime=true;i.Calendar=false;i.Title="";ReminderSchedule.Validate(b);
    }
    [Fact]public void LegacyMigrationKeepsNotesAndDisabledEarlyChoice()
    {
        ReminderBook b=new(){ShowUpcoming=false};var i=new ReminderItem{Title="old",Notes=new string('x',1000),Calendar=true,ShowCountdown=true,PendingAt=Day.AddHours(14),Start=Day.AddHours(14)};b.Items.Add(i);ReminderEngine.Initialize(b,Day);Assert.False(i.EarlyEnabled);Assert.Equal(1000,i.Notes.Length);Assert.True(i.ReminderCreated);Assert.Equal(ReminderPhase.Due,Assert.Single(b.Occurrences).Phase);ReminderEngine.Initialize(b,Day);Assert.Single(b.Occurrences);
    }
}
