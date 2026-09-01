using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class PetStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void CompletingReactionReResolvesCurrentMusicState()
    {
        PetStateMachine machine = new(new PetVisualState(PetDisplayMode.FullBodyInteractive));
        machine.SetMusicAnimation(PetVisualState.EnjoyMusicAnimation);
        machine.SetContinuousState(PetContinuousState.MusicPlaying);
        ReactionStartOutcome outcome = machine.TryStartReaction(Request("notification"), Now);

        Assert.Equal("notification", machine.Resolve(Now).AnimationId);
        Assert.True(machine.CompleteReaction(outcome.Token!.Value, Now.AddSeconds(2)));
        Assert.Equal(PetVisualState.EnjoyMusicAnimation, machine.Resolve(Now.AddSeconds(2)).AnimationId);
        Assert.Equal(PetDisplayMode.FullBodyInteractive, machine.VisualState.SelectedDisplayMode);
    }

    [Fact]
    public void MusicSelectionChangedDuringDragIsUsedAfterDrop()
    {
        PetStateMachine machine = new();
        Assert.True(machine.BeginDrag());

        machine.SetMusicAnimation(PetVisualState.EnjoyMusicAnimation);
        machine.SetContinuousState(PetContinuousState.MusicPlaying);

        Assert.True(machine.EndDrag());
        Assert.Equal(PetVisualState.EnjoyMusicAnimation, machine.Resolve(Now).AnimationId);
    }

    [Theory]
    [InlineData(PetDisplayMode.Compact)]
    [InlineData(PetDisplayMode.FullBodyInteractive)]
    public void MusicAnimationRemainsVisibleThroughoutDrag(PetDisplayMode displayMode)
    {
        PetStateMachine machine = new(new PetVisualState(
            displayMode,
            PetContinuousState.MusicPlaying,
            PetVisualState.EnjoyMusicAnimation));

        Assert.True(machine.BeginDrag());
        PetPlaybackPlan draggingPlan = machine.Resolve(Now);

        Assert.Equal(PetVisualState.EnjoyMusicAnimation, draggingPlan.AnimationId);
        Assert.False(draggingPlan.BodyRegionInteractionsEnabled);
        Assert.True(machine.EndDrag());
        Assert.Equal(PetVisualState.EnjoyMusicAnimation, machine.Resolve(Now).AnimationId);
    }

    [Fact]
    public void HigherPriorityReplacesAndStaleCompletionCannotEndReplacement()
    {
        PetStateMachine machine = new();
        ReactionStartOutcome notification = machine.TryStartReaction(
            Request("notification", ReactionPriority.Notification), Now);
        ReactionStartOutcome genshin = machine.TryStartReaction(
            Request("genshin", ReactionPriority.Genshin), Now);

        Assert.Equal(ReactionStartResult.Replaced, genshin.Result);
        Assert.False(machine.CompleteReaction(notification.Token!.Value, Now));
        Assert.Equal("genshin", machine.Resolve(Now).AnimationId);
    }

    [Fact]
    public void LowerPriorityReactionIsRejected()
    {
        PetStateMachine machine = new();
        machine.TryStartReaction(Request("notification", ReactionPriority.Notification), Now);

        ReactionStartOutcome result = machine.TryStartReaction(
            Request("media", ReactionPriority.MediaOrVolume), Now);

        Assert.Equal(ReactionStartResult.RejectedByPriority, result.Result);
        Assert.Equal("notification", machine.Resolve(Now).AnimationId);
    }

    [Fact]
    public void SameMergeKeyMergesAndCooldownSuppressesLaterReaction()
    {
        PetStateMachine machine = new();
        ReactionRequest first = Request("message", mergeKey: "im-message", cooldown: TimeSpan.FromMinutes(1));
        ReactionStartOutcome started = machine.TryStartReaction(first, Now);
        ReactionStartOutcome merged = machine.TryStartReaction(
            first with { ExpiresAt = Now.AddSeconds(20) }, Now.AddSeconds(1));

        Assert.Equal(ReactionStartResult.Merged, merged.Result);
        Assert.Equal(started.Token, merged.Token);
        Assert.True(machine.CompleteReaction(started.Token!.Value, Now.AddSeconds(2)));
        Assert.Equal(
            ReactionStartResult.SuppressedByCooldown,
            machine.TryStartReaction(first with { ExpiresAt = Now.AddMinutes(2) }, Now.AddSeconds(30)).Result);
        Assert.Equal(
            ReactionStartResult.Started,
            machine.TryStartReaction(first with { ExpiresAt = Now.AddMinutes(2) }, Now.AddMinutes(1).AddSeconds(3)).Result);
    }

    [Fact]
    public void ExpiredReactionIsDropped()
    {
        PetStateMachine machine = new();

        ReactionStartOutcome result = machine.TryStartReaction(
            Request("stale") with { ExpiresAt = Now }, Now);

        Assert.Equal(ReactionStartResult.Expired, result.Result);
        Assert.Equal(PetVisualState.CompactIdleAnimation, machine.Resolve(Now).AnimationId);
    }

    [Fact]
    public void CompactDragInterruptsReactionAndRestoresStateUpdatedDuringDrag()
    {
        PetStateMachine machine = new();
        machine.TryStartReaction(Request("ordinary"), Now);

        Assert.True(machine.BeginDrag());
        Assert.Equal(PetVisualState.CompactDraggingAnimation, machine.Resolve(Now).AnimationId);
        machine.SetContinuousState(PetContinuousState.MusicPlaying);
        Assert.True(machine.EndDrag());
        Assert.Equal(PetVisualState.MusicSwayAnimation, machine.Resolve(Now).AnimationId);
    }

    [Fact]
    public void FullBodyDragKeepsFullBodyIdleVisualAndDisablesBodyRegions()
    {
        PetStateMachine machine = new(new PetVisualState(PetDisplayMode.FullBodyInteractive));

        Assert.True(machine.Resolve(Now).BodyRegionInteractionsEnabled);
        Assert.True(machine.BeginDrag());
        PetPlaybackPlan plan = machine.Resolve(Now);

        Assert.Equal(PetVisualState.FullBodyIdleAnimation, plan.AnimationId);
        Assert.False(plan.BodyRegionInteractionsEnabled);
    }

    [Fact]
    public void BodyInteractionRecoveryDelayDoesNotBlockDoubleClickStateChangeOrDrag()
    {
        PetStateMachine machine = new(new PetVisualState(PetDisplayMode.FullBodyInteractive));
        machine.SuppressBodyInteractions(Now, TimeSpan.FromMilliseconds(800));

        Assert.False(machine.Resolve(Now.AddMilliseconds(799)).BodyRegionInteractionsEnabled);
        machine.SetDisplayMode(PetDisplayMode.Compact);
        Assert.Equal(PetDisplayMode.Compact, machine.VisualState.SelectedDisplayMode);
        machine.SetDisplayMode(PetDisplayMode.FullBodyInteractive);
        Assert.True(machine.BeginDrag());
        Assert.True(machine.EndDrag());
        Assert.True(machine.Resolve(Now.AddMilliseconds(800)).BodyRegionInteractionsEnabled);
    }

    [Fact]
    public void LaterBodyInteractionSuppressionExtendsExistingRecoveryDelay()
    {
        PetStateMachine machine = new(new PetVisualState(PetDisplayMode.FullBodyInteractive));
        machine.SuppressBodyInteractions(Now, TimeSpan.FromMilliseconds(800));

        machine.SuppressBodyInteractions(Now.AddMilliseconds(400), TimeSpan.FromMilliseconds(800));

        Assert.False(machine.Resolve(Now.AddMilliseconds(1199)).BodyRegionInteractionsEnabled);
        Assert.True(machine.Resolve(Now.AddMilliseconds(1200)).BodyRegionInteractionsEnabled);
    }

    [Fact]
    public void NonInterruptibleReactionBlocksDrag()
    {
        PetStateMachine machine = new();
        machine.TryStartReaction(
            Request("exit", ReactionPriority.Exit) with { InterruptibleByDrag = false }, Now);

        Assert.False(machine.BeginDrag());
        Assert.Equal("exit", machine.Resolve(Now).AnimationId);
    }

    [Fact]
    public void HiddenForSafetyProducesInvisiblePlan()
    {
        PetStateMachine machine = new();
        machine.SetContinuousState(PetContinuousState.HiddenForSafety);

        PetPlaybackPlan plan = machine.Resolve(Now);

        Assert.False(plan.IsVisible);
        Assert.Null(plan.AnimationId);
        Assert.False(machine.BeginDrag());
    }

    [Fact]
    public void HiddenForSafetyImmediatelyEndsAnActiveDrag()
    {
        PetStateMachine machine = new();
        Assert.True(machine.BeginDrag());

        machine.SetContinuousState(PetContinuousState.HiddenForSafety);

        Assert.False(machine.Resolve(Now).IsVisible);
        Assert.False(machine.EndDrag());
    }

    private static ReactionRequest Request(
        string animationId,
        ReactionPriority priority = ReactionPriority.UserInteraction,
        string? mergeKey = null,
        TimeSpan cooldown = default) => new(
            animationId,
            priority,
            Now.AddMinutes(1),
            mergeKey,
            cooldown);
}
