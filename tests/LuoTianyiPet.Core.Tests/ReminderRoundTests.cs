using LuoTianyiPet.Core;
namespace LuoTianyiPet.Core.Tests;
public sealed class ReminderRoundTests
{
    [Theory][InlineData(true,true)][InlineData(true,false)][InlineData(false,true)]
    public void EachEnabledPresentationStopsAfterSongAndRetriesInTenMinutes(bool sound,bool animation)
    {
        DateTime now=new(2026,9,16,14,0,0);
        ReminderItem rule=new(){Start=now,CheckedThrough=now.AddMinutes(-1),Repeat=ReminderRepeat.Daily};
        ReminderBook book=new(){EngineVersion=1,Items=[rule],Preferences=new(){Sound=sound,Animation=animation}};
        ReminderEngine.Advance(book,now);var occurrence=Assert.Single(book.Occurrences);
        ReminderEngine.Advance(book,now.AddSeconds(221));Assert.Equal(ReminderPhase.Due,occurrence.Phase);
        ReminderEngine.Advance(book,now.AddSeconds(222));Assert.Equal(ReminderPhase.DueSnoozed,occurrence.Phase);
        Assert.Equal(now.AddSeconds(822),occurrence.SnoozeAt);
        ReminderEngine.Advance(book,now.AddSeconds(821));Assert.Equal(ReminderPhase.DueSnoozed,occurrence.Phase);
        ReminderEngine.Advance(book,now.AddSeconds(822));Assert.Equal(ReminderPhase.Due,occurrence.Phase);
        Assert.Equal(now.AddSeconds(822),occurrence.RoundStartedAt);
        ReminderEngine.Acknowledge(book,rule.Id,now,ReminderPhase.Due);
        ReminderEngine.Advance(book,now.AddHours(1));Assert.Equal(ReminderPhase.Done,occurrence.Phase);
        ReminderEngine.Advance(book,now.AddDays(1));Assert.Equal(2,book.Occurrences.Count);
    }
    [Fact]public void SilentCardWithoutAnimationDoesNotAutoRepeat()
    {
        DateTime now=new(2026,9,16,14,0,0);ReminderItem r=new(){Start=now,CheckedThrough=now.AddSeconds(-1)};
        ReminderBook b=new(){EngineVersion=1,Items=[r],Preferences=new(){Sound=false,Animation=false}};
        ReminderEngine.Advance(b,now);ReminderEngine.Advance(b,now.AddMinutes(5));Assert.Equal(ReminderPhase.Due,Assert.Single(b.Occurrences).Phase);
    }
    [Fact]public void ConcurrentRemindersHaveIndependentRoundDeadlines()
    {
        DateTime now=new(2026,9,16,14,0,0);ReminderItem first=new(){Start=now,CheckedThrough=now.AddSeconds(-1)},second=new(){Start=now.AddMinutes(2),CheckedThrough=now.AddSeconds(-1)};
        ReminderBook b=new(){EngineVersion=1,Items=[first,second]};ReminderEngine.Advance(b,now);ReminderEngine.Advance(b,now.AddMinutes(2));ReminderEngine.Advance(b,now.AddSeconds(222));
        Assert.Equal(ReminderPhase.DueSnoozed,b.Occurrences[0].Phase);Assert.Equal(ReminderPhase.Due,b.Occurrences[1].Phase);
    }
}
