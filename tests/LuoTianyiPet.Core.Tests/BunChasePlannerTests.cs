using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class BunChasePlannerTests
{
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
}
