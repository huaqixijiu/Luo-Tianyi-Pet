using LuoTianyiPet.Core;
namespace LuoTianyiPet.Core.Tests;
public sealed class DragPreservationTests
{
    [Theory]
    [InlineData(PetContinuousState.Idle)]
    [InlineData(PetContinuousState.MediumIdleCountdown)]
    [InlineData(PetContinuousState.MediumIdle)]
    [InlineData(PetContinuousState.Sleeping)]
    [InlineData(PetContinuousState.MusicPlaying)]
    public void MovementRetainsUnderlyingStateAndPlayback(PetContinuousState state)
    {
        var machine = new PetStateMachine(new PetVisualState(PetDisplayMode.FullBodyInteractive, state));
        var before = machine.Resolve(DateTimeOffset.Now);
        Assert.True(machine.BeginDrag());
        Assert.Equal(state, machine.CurrentContinuousState);
        Assert.Equal(before.AnimationId, machine.Resolve(DateTimeOffset.Now).AnimationId);
        Assert.True(machine.EndDrag());
        Assert.Equal(state, machine.CurrentContinuousState);
    }
    [Fact]
    public void ReactionCanFinishNaturallyWhileMoving()
    {
        var machine = new PetStateMachine();
        var now = DateTimeOffset.Now;
        var reaction = machine.TryStartReaction(new("body", ReactionPriority.UserInteraction, now.AddSeconds(5)), now);
        machine.BeginDrag();
        Assert.Equal(reaction.Token, machine.ActiveReactionToken);
        Assert.True(machine.CompleteReaction(reaction.Token!.Value, now.AddSeconds(1)));
        Assert.Equal(PetVisualState.CompactIdleAnimation, machine.Resolve(now.AddSeconds(1)).AnimationId);
        Assert.True(machine.EndDrag());
    }
    [Fact]
    public void CountdownCanFinishWithoutDroppingTheDrag()
    {
        var machine = new PetStateMachine(new PetVisualState(ContinuousState: PetContinuousState.MediumIdleCountdown));
        machine.BeginDrag();
        machine.SetContinuousState(PetContinuousState.MediumIdle);
        Assert.True(machine.IsDraggingHehe);
        Assert.Equal(PetVisualState.MediumIdleAnimation, machine.Resolve(DateTimeOffset.Now).AnimationId);
        Assert.True(machine.EndDrag());
    }
    [Fact]
    public void ChaseAccelerationIsSmoothMonotonicAndBoundedAtBothEnds()
    {
        double Speed(double t) => BunChasePlanner.ResolveSpeedTowardMaximum(180,800,TimeSpan.FromTicks((long)(t*TimeSpan.TicksPerSecond)),TimeSpan.FromSeconds(3.5));
        double previous = Speed(0);
        for (int frame=1;frame<=240;frame++)
        {
            double speed = Speed(frame/60d);
            Assert.InRange(speed,previous,800);
            Assert.InRange(speed-previous,0,4.5);
            previous=speed;
        }
        Assert.True(Speed(.05)-Speed(0)<1);
        Assert.True(Speed(3.5)-Speed(3.45)<1);
        Assert.Equal(800, Speed(100));
        Assert.Equal(180, Speed(-1));
    }
}
