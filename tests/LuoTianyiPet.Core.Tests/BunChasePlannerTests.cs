using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class BunChasePlannerTests
{
    [Theory]
    [InlineData(false, 140)]
    [InlineData(true, 100)]
    public void ResolveMouthTarget_UsesCalibratedAndMirroredOpenMouthPosition(
        bool mirrored,
        double expectedX)
    {
        PointerPoint mouth = BunChasePlanner.ResolveMouthTarget(
            new PointerPoint(20, 30),
            200,
            300,
            mirrored);

        Assert.Equal(expectedX, mouth.X, 3);
        Assert.Equal(190.5, mouth.Y, 3);
    }

    [Theory]
    [InlineData(false, 145)]
    [InlineData(true, 95)]
    public void ResolveMouthTarget_UsesPerStyleCalibration(
        bool mirrored,
        double expectedX)
    {
        PointerPoint mouth = BunChasePlanner.ResolveMouthTarget(
            new PointerPoint(20, 30),
            200,
            300,
            mirrored,
            unmirroredXFraction: 0.625,
            yFraction: 0.452);

        Assert.Equal(expectedX, mouth.X, 3);
        Assert.Equal(165.6, mouth.Y, 3);
    }

    [Fact]
    public void DesktopFileTreatSafety_AllowsScreenSizedExplorerDesktop()
    {
        bool allowed = DesktopFileTreatSafety.AllowsForeground(
            new ForegroundApplicationSnapshot(true, "explorer", true),
            protectedApplicationForeground: false);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(false, "explorer", true, false)]
    [InlineData(true, "chrome", true, false)]
    [InlineData(true, "YuanShen", true, true)]
    public void DesktopFileTreatSafety_RejectsUnsafeForegrounds(
        bool succeeded,
        string processName,
        bool fullscreen,
        bool protectedApplicationForeground)
    {
        bool allowed = DesktopFileTreatSafety.AllowsForeground(
            new ForegroundApplicationSnapshot(succeeded, processName, fullscreen),
            protectedApplicationForeground);

        Assert.False(allowed);
    }

    [Fact]
    public void Advance_MovesTowardTargetAtConfiguredSpeed()
    {
        BunChaseStep step = BunChasePlanner.Advance(
            new PointerPoint(0, 0),
            new PointerPoint(100, 0),
            50,
            TimeSpan.FromSeconds(1),
            5);

        Assert.Equal(50, step.Position.X, 3);
        Assert.Equal(0, step.Position.Y, 3);
        Assert.False(step.Arrived);
    }

    [Fact]
    public void Advance_StopsAtArrivalRadiusWithoutOvershooting()
    {
        BunChaseStep step = BunChasePlanner.Advance(
            new PointerPoint(0, 0),
            new PointerPoint(10, 0),
            500,
            TimeSpan.FromSeconds(1),
            3);

        Assert.Equal(7, step.Position.X, 3);
        Assert.True(step.Arrived);
    }

    [Fact]
    public void AdvanceSpeed_EasesFromReducedStartTowardCruiseSpeed()
    {
        double first = BunChasePlanner.AdvanceSpeed(72, 270, 360, TimeSpan.FromMilliseconds(100));
        double later = BunChasePlanner.AdvanceSpeed(first, 270, 360, TimeSpan.FromSeconds(1));

        Assert.Equal(108, first, 3);
        Assert.Equal(270, later, 3);
    }
}
