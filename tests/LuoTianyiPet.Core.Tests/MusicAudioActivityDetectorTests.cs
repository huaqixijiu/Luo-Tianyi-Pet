using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class MusicAudioActivityDetectorTests
{
    [Fact]
    public void Update_WithInvalidPeak_RejectsSnapshot()
    {
        MusicAudioActivityDetector detector = CreateDetector();

        Assert.Throws<ArgumentOutOfRangeException>(() => detector.Update(
            new AudioSessionSnapshot(true, true, float.NaN),
            DateTimeOffset.UtcNow));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void AudibleTargetSessionStartsPlaybackOnce()
    {
        MusicAudioActivityDetector detector = CreateDetector();

        Assert.Equal(MusicActivityTransition.Started, detector.Update(AudioSessionSnapshot.Found(0.2f), Now));
        Assert.Equal(
            MusicActivityTransition.None,
            detector.Update(AudioSessionSnapshot.Found(0.3f), Now.AddMilliseconds(250)));
        Assert.True(detector.IsPlaying);
    }

    [Fact]
    public void ShortSilenceDoesNotFlapBetweenSongs()
    {
        MusicAudioActivityDetector detector = CreateDetector();
        detector.Update(AudioSessionSnapshot.Found(0.2f), Now);

        Assert.Equal(
            MusicActivityTransition.None,
            detector.Update(AudioSessionSnapshot.Found(0), Now.AddMilliseconds(499)));
        Assert.True(detector.IsPlaying);
        Assert.Equal(
            MusicActivityTransition.Stopped,
            detector.Update(AudioSessionSnapshot.Found(0), Now.AddMilliseconds(500)));
    }

    [Fact]
    public void NewSoundDuringGraceRestartsTheSilenceClock()
    {
        MusicAudioActivityDetector detector = CreateDetector();
        detector.Update(AudioSessionSnapshot.Found(0.2f), Now);
        detector.Update(AudioSessionSnapshot.Found(0), Now.AddMilliseconds(300));
        detector.Update(AudioSessionSnapshot.Found(0.2f), Now.AddMilliseconds(400));

        Assert.Equal(
            MusicActivityTransition.None,
            detector.Update(AudioSessionSnapshot.Found(0), Now.AddMilliseconds(899)));
        Assert.Equal(
            MusicActivityTransition.Stopped,
            detector.Update(AudioSessionSnapshot.Found(0), Now.AddMilliseconds(900)));
    }

    [Fact]
    public void MissingTargetSessionStopsImmediately()
    {
        MusicAudioActivityDetector detector = CreateDetector();
        detector.Update(AudioSessionSnapshot.Found(0.2f), Now);

        Assert.Equal(
            MusicActivityTransition.Stopped,
            detector.Update(AudioSessionSnapshot.Missing, Now.AddMilliseconds(250)));
    }

    [Fact]
    public void TransientProbeFailurePreservesCurrentState()
    {
        MusicAudioActivityDetector detector = CreateDetector();
        detector.Update(AudioSessionSnapshot.Found(0.2f), Now);

        Assert.Equal(
            MusicActivityTransition.None,
            detector.Update(AudioSessionSnapshot.Unavailable, Now.AddSeconds(10)));
        Assert.True(detector.IsPlaying);
    }

    [Fact]
    public void SilentSessionDoesNotStartPlayback()
    {
        MusicAudioActivityDetector detector = CreateDetector();

        Assert.Equal(MusicActivityTransition.None, detector.Update(AudioSessionSnapshot.Found(0), Now));
        Assert.False(detector.IsPlaying);
    }

    private static MusicAudioActivityDetector CreateDetector() => new(
        0.001f,
        TimeSpan.FromMilliseconds(500));
}
