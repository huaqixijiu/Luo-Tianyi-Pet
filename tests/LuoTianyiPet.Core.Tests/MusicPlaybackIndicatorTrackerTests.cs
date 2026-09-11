using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class MusicPlaybackIndicatorTrackerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void ExternalSilenceUpdatesIndicatorBeforeAnimationGraceExpires()
    {
        MusicPlaybackIndicatorTracker tracker = CreateTracker(initiallyPlaying: true);

        Assert.False(tracker.Observe(AudioSessionSnapshot.Found(0), Now));
        Assert.True(tracker.IsPlaying);
        Assert.True(tracker.Observe(
            AudioSessionSnapshot.Found(0),
            Now.AddMilliseconds(750)));
        Assert.False(tracker.IsPlaying);
    }

    [Fact]
    public void ShortAudioGapDoesNotChangeIndicator()
    {
        MusicPlaybackIndicatorTracker tracker = CreateTracker(initiallyPlaying: true);

        Assert.False(tracker.Observe(AudioSessionSnapshot.Found(0), Now));
        Assert.False(tracker.Observe(
            AudioSessionSnapshot.Found(0.2f),
            Now.AddMilliseconds(500)));
        Assert.True(tracker.IsPlaying);
    }

    [Fact]
    public void UserPauseExpectationChangesImmediatelyAndIgnoresCommandLag()
    {
        MusicPlaybackIndicatorTracker tracker = CreateTracker(initiallyPlaying: true);

        Assert.True(tracker.Expect(false, Now, TimeSpan.FromSeconds(2)));
        Assert.False(tracker.IsPlaying);
        Assert.False(tracker.Observe(
            AudioSessionSnapshot.Found(0.2f),
            Now.AddMilliseconds(250)));
        Assert.False(tracker.IsPlaying);
        Assert.False(tracker.Observe(
            AudioSessionSnapshot.Found(0),
            Now.AddMilliseconds(500)));
        Assert.False(tracker.IsPlaying);
    }

    [Fact]
    public void FailedUserPauseRevertsToAudibleStateAfterExpectationExpires()
    {
        MusicPlaybackIndicatorTracker tracker = CreateTracker(initiallyPlaying: true);
        tracker.Expect(false, Now, TimeSpan.FromSeconds(2));

        Assert.True(tracker.Observe(
            AudioSessionSnapshot.Found(0.2f),
            Now.AddSeconds(2)));
        Assert.True(tracker.IsPlaying);
    }

    [Fact]
    public void UserPlayExpectationChangesImmediately()
    {
        MusicPlaybackIndicatorTracker tracker = CreateTracker(initiallyPlaying: false);

        Assert.True(tracker.Expect(true, Now, TimeSpan.FromSeconds(2)));
        Assert.True(tracker.IsPlaying);
    }

    [Fact]
    public void FailedUserPlayRevertsToSilentStateAfterExpectationExpires()
    {
        MusicPlaybackIndicatorTracker tracker = CreateTracker(initiallyPlaying: false);
        tracker.Expect(true, Now, TimeSpan.FromSeconds(2));

        Assert.True(tracker.Observe(
            AudioSessionSnapshot.Found(0),
            Now.AddSeconds(2)));
        Assert.False(tracker.IsPlaying);
    }

    [Fact]
    public void ProbeFailureKeepsCurrentIndicatorState()
    {
        MusicPlaybackIndicatorTracker tracker = CreateTracker(initiallyPlaying: true);

        Assert.False(tracker.Observe(AudioSessionSnapshot.Unavailable, Now));
        Assert.True(tracker.IsPlaying);
    }

    private static MusicPlaybackIndicatorTracker CreateTracker(bool initiallyPlaying) => new(
        MediaPreferences.DefaultAudiblePeakThreshold,
        MusicPlaybackIndicatorTracker.DefaultSilenceDelay,
        initiallyPlaying);
}
