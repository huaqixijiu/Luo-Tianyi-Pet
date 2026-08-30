using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class CoreAudioSessionProbeTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyProcessNameIsRejected(string processName)
    {
        CoreAudioSessionProbe probe = new();

        Assert.Throws<ArgumentException>(() => probe.ReadForProcess(processName));
    }

    [Fact]
    public void MissingWhitelistedProcessDoesNotRequireAnAudioDevice()
    {
        CoreAudioSessionProbe probe = new();

        AudioSessionSnapshot snapshot = probe.ReadForProcess(
            $"missing-luotianyi-pet-process-{Guid.NewGuid():N}.exe");

        Assert.True(snapshot.ProbeSucceeded);
        Assert.False(snapshot.TargetSessionFound);
        Assert.Equal(0, snapshot.PeakLevel);
    }
}
