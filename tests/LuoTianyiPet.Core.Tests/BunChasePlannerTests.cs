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
    [InlineData("explorer")]
    [InlineData("explorer.exe")]
    [InlineData(@"C:\Windows\explorer.exe")]
    public void DesktopFileTreatSafety_AllowsExplorerShellAndFolderWindows(string processName)
    {
        bool allowed = DesktopFileTreatSafety.AllowsForeground(
            new ForegroundApplicationSnapshot(true, processName, true),
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

    [Theory]
    [InlineData(0, 180)]
    [InlineData(1.75, 490)]
    [InlineData(3.5, 800)]
    [InlineData(10, 800)]
    public void ResolveSpeedTowardMaximum_ReachesExplicitCapAtThreePointFiveSeconds(
        double elapsedSeconds,
        double expectedSpeed)
    {
        double speed = BunChasePlanner.ResolveSpeedTowardMaximum(
            180,
            800,
            TimeSpan.FromSeconds(elapsedSeconds),
            TimeSpan.FromSeconds(3.5));

        Assert.Equal(expectedSpeed, speed, 3);
    }

    [Theory]
    [InlineData(1366, 768, 1.0)]
    [InlineData(1920, 1080, 1.0)]
    [InlineData(2560, 1440, 1.333333)]
    [InlineData(3840, 2160, 2.0)]
    public void ResolveDesktopSpeedScale_NormalizesLargeLogicalDesktops(
        double width,
        double height,
        double expectedScale)
    {
        Assert.Equal(
            expectedScale,
            BunChasePlanner.ResolveDesktopSpeedScale(width, height),
            5);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3840, 2160)]
    public void DiagonalChase_HasConsistentTravelTimeAcrossDesktopResolutions(
        double width,
        double height)
    {
        double scale = BunChasePlanner.ResolveDesktopSpeedScale(width, height);
        double diagonal = Math.Sqrt(width * width + height * height);
        TimeSpan duration = BunChasePlanner.EstimateTravelDuration(
            diagonal,
            180 * scale,
            800 * scale,
            TimeSpan.FromSeconds(3.5));

        Assert.InRange(duration.TotalSeconds, 4.10, 4.12);
    }

    [Fact]
    public void FourKAtTwoHundredPercent_UsesReferenceLogicalDesktopTiming()
    {
        double scale = BunChasePlanner.ResolveDesktopSpeedScaleFromPixels(
            3840,
            2160,
            2,
            2);
        TimeSpan duration = BunChasePlanner.EstimateTravelDuration(
            Math.Sqrt(1920 * 1920 + 1080 * 1080),
            180 * scale,
            800 * scale,
            TimeSpan.FromSeconds(3.5));

        Assert.Equal(1, scale);
        Assert.InRange(duration.TotalSeconds, 4.10, 4.12);
    }

    [Theory]
    [InlineData(true, true, false, 1, true)]
    [InlineData(true, true, false, 3, true)]
    [InlineData(false, true, false, 1, false)]
    [InlineData(true, false, false, 1, false)]
    [InlineData(true, true, true, 1, false)]
    [InlineData(true, true, false, 0, false)]
    public void QueuedTreat_InterruptsOnlyAnActiveNonEatingReturn(
        bool chaseActive,
        bool returning,
        bool eating,
        int queuedBunCount,
        bool expected)
    {
        Assert.Equal(
            expected,
            BunChasePlanner.ShouldInterruptReturnForQueuedTreat(
                chaseActive,
                returning,
                eating,
                queuedBunCount));
    }

    [Theory]
    [InlineData(1, true, true, 3, false, true)]
    [InlineData(1, true, true, 2.99, false, false)]
    [InlineData(2, true, true, 10, false, false)]
    [InlineData(1, false, true, 10, false, false)]
    [InlineData(1, true, false, 10, false, false)]
    [InlineData(1, true, true, 10, true, false)]
    public void BunRequest_RequiresOneContinuouslyDraggedBunAtMaximumSpeed(
        int count,
        bool atMaximum,
        bool dragging,
        double dragSeconds,
        bool alreadyShown,
        bool expected)
    {
        Assert.Equal(
            expected,
            BunChasePlanner.ShouldShowBunRequest(
                count,
                atMaximum,
                dragging,
                TimeSpan.FromSeconds(dragSeconds),
                TimeSpan.FromSeconds(3),
                alreadyShown));
    }

    [Fact]
    public void FeedHit_AcceptsOpaqueHairAroundATransparentCentreGap()
    {
        byte[] alpha = new byte[10 * 10];
        for (int y = 2; y <= 7; y++)
        {
            alpha[y * 10 + 2] = 255;
            alpha[y * 10 + 7] = 255;
        }

        bool accepted = BunFeedHitTester.HasOpaqueOverlap(
            alpha,
            10,
            10,
            new PointerPoint(0, 0),
            100,
            100,
            new PointerPoint(20, 20),
            60,
            60);

        Assert.True(accepted);
    }

    [Fact]
    public void FeedHit_RejectsTreatCompletelyInsideTransparentArea()
    {
        byte[] alpha = new byte[10 * 10];
        alpha[0] = 255;

        bool accepted = BunFeedHitTester.HasOpaqueOverlap(
            alpha,
            10,
            10,
            new PointerPoint(0, 0),
            100,
            100,
            new PointerPoint(40, 40),
            20,
            20);

        Assert.False(accepted);
    }
}
