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

    [Theory]
    [InlineData(-0.01f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.42f, 0.42f)]
    [InlineData(1f, 1f)]
    [InlineData(1.01f, 1f)]
    public void TryNormalizePeakLevel_ClampsFiniteTransientValues(
        float value,
        float expected)
    {
        bool accepted = CoreAudioSessionProbe.TryNormalizePeakLevel(value, out float normalized);

        Assert.True(accepted);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void TryNormalizePeakLevel_RejectsNonFiniteValues(float value)
    {
        bool accepted = CoreAudioSessionProbe.TryNormalizePeakLevel(value, out float normalized);

        Assert.False(accepted);
        Assert.Equal(0, normalized);
    }
}
