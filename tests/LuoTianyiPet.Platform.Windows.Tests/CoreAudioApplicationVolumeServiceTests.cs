using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class CoreAudioApplicationVolumeServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyProcessNameIsRejected(string processName)
    {
        Assert.Throws<ArgumentException>(() =>
            new CoreAudioApplicationVolumeService(processName, new SafetyPreferences()));
    }

    [Fact]
    public void MissingProcessDoesNotRequireAnAudioDevice()
    {
        using CoreAudioApplicationVolumeService service = new(
            $"missing-luotianyi-volume-{Guid.NewGuid():N}.exe",
            new SafetyPreferences());

        ApplicationVolumeSnapshot snapshot = service.Read();

        Assert.True(snapshot.ProbeSucceeded);
        Assert.False(snapshot.TargetSessionFound);
        Assert.Equal(0, snapshot.SessionCount);
    }

    [Fact]
    public void MissingProcessCannotBeAdjusted()
    {
        using CoreAudioApplicationVolumeService service = new(
            $"missing-luotianyi-volume-{Guid.NewGuid():N}.exe",
            new SafetyPreferences());

        ApplicationVolumeAdjustmentResult result = service.TrySetLevel(0.5f);

        Assert.Contains(
            result.Status,
            new[]
            {
                ApplicationVolumeAdjustmentStatus.TargetSessionMissing,
                ApplicationVolumeAdjustmentStatus.ForegroundCheckUnavailable,
            });
    }
}
