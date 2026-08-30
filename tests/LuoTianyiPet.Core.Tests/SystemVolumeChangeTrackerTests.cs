using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class SystemVolumeChangeTrackerTests
{
    [Fact]
    public void Observe_FirstAvailableSnapshot_EstablishesBaselineWithoutFeedback()
    {
        SystemVolumeChangeTracker tracker = new();

        SystemVolumeFeedbackDecision decision = tracker.Observe(
            SystemVolumeSnapshot.Available(0.42f, false));

        Assert.False(decision.ShouldShow);
        Assert.Equal(SystemVolumeChangeKind.None, decision.Kind);
        Assert.Equal(42, decision.Snapshot.Percentage);
    }

    [Fact]
    public void Observe_LevelChanges_ClassifiesDirection()
    {
        SystemVolumeChangeTracker tracker = new();
        tracker.Observe(SystemVolumeSnapshot.Available(0.4f, false));

        SystemVolumeFeedbackDecision increased = tracker.Observe(
            SystemVolumeSnapshot.Available(0.42f, false));
        SystemVolumeFeedbackDecision decreased = tracker.Observe(
            SystemVolumeSnapshot.Available(0.39f, false));

        Assert.Equal(SystemVolumeChangeKind.Increased, increased.Kind);
        Assert.True(increased.ShouldAnimate);
        Assert.Equal(SystemVolumeChangeKind.Decreased, decreased.Kind);
        Assert.True(decreased.ShouldAnimate);
    }

    [Fact]
    public void Observe_MuteChanges_ShowFeedbackWithoutAnimation()
    {
        SystemVolumeChangeTracker tracker = new();
        tracker.Observe(SystemVolumeSnapshot.Available(0.4f, false));

        SystemVolumeFeedbackDecision muted = tracker.Observe(
            SystemVolumeSnapshot.Available(0.4f, true));
        SystemVolumeFeedbackDecision unmuted = tracker.Observe(
            SystemVolumeSnapshot.Available(0.4f, false));

        Assert.Equal(SystemVolumeChangeKind.Muted, muted.Kind);
        Assert.True(muted.ShouldShow);
        Assert.False(muted.ShouldAnimate);
        Assert.Equal(SystemVolumeChangeKind.Unmuted, unmuted.Kind);
        Assert.False(unmuted.ShouldAnimate);
    }

    [Fact]
    public void Observe_UnavailableSnapshot_DoesNotDiscardLastAvailableBaseline()
    {
        SystemVolumeChangeTracker tracker = new();
        tracker.Observe(SystemVolumeSnapshot.Available(0.4f, false));

        Assert.False(tracker.Observe(SystemVolumeSnapshot.Unavailable).ShouldShow);
        SystemVolumeFeedbackDecision decision = tracker.Observe(
            SystemVolumeSnapshot.Available(0.5f, false));

        Assert.Equal(SystemVolumeChangeKind.Increased, decision.Kind);
    }
}
