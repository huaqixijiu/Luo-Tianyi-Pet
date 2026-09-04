using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class ApplicationVolumeTests
{
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.425f, 43)]
    [InlineData(1f, 100)]
    public void FoundSnapshotClampsAndRoundsPercentage(float level, int expected)
    {
        ApplicationVolumeSnapshot snapshot = ApplicationVolumeSnapshot.Found(level, 2);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(expected, snapshot.Percentage);
        Assert.Equal(2, snapshot.SessionCount);
    }

    [Fact]
    public void MissingAndUnavailableAreDistinct()
    {
        Assert.True(ApplicationVolumeSnapshot.Missing.ProbeSucceeded);
        Assert.False(ApplicationVolumeSnapshot.Missing.TargetSessionFound);
        Assert.False(ApplicationVolumeSnapshot.Unavailable.ProbeSucceeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FoundSnapshotRejectsInvalidSessionCount(int sessionCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ApplicationVolumeSnapshot.Found(0.5f, sessionCount));
    }
}
