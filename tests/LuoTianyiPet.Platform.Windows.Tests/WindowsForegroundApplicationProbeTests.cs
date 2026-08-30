using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class WindowsForegroundApplicationProbeTests
{
    private static readonly NativeRectangle Monitor = new()
    {
        Left = 0,
        Top = 0,
        Right = 1920,
        Bottom = 1080,
    };

    [Fact]
    public void CoversMonitor_AcceptsExactMonitorBounds()
    {
        Assert.True(WindowsForegroundApplicationProbe.CoversMonitor(Monitor, Monitor));
    }

    [Fact]
    public void CoversMonitor_AcceptsSmallWindowManagerRoundingDifference()
    {
        NativeRectangle roundedWindow = new()
        {
            Left = 1,
            Top = 2,
            Right = 1918,
            Bottom = 1079,
        };

        Assert.True(WindowsForegroundApplicationProbe.CoversMonitor(roundedWindow, Monitor));
    }

    [Fact]
    public void CoversMonitor_RejectsOrdinaryMaximizedWorkAreaWindow()
    {
        NativeRectangle workAreaWindow = new()
        {
            Left = 0,
            Top = 0,
            Right = 1920,
            Bottom = 1040,
        };

        Assert.False(WindowsForegroundApplicationProbe.CoversMonitor(workAreaWindow, Monitor));
    }

    [Fact]
    public void CoversMonitor_RejectsWindowThatOnlySpansPartOfMonitor()
    {
        NativeRectangle partialWindow = new()
        {
            Left = 300,
            Top = 150,
            Right = 1620,
            Bottom = 930,
        };

        Assert.False(WindowsForegroundApplicationProbe.CoversMonitor(partialWindow, Monitor));
    }

    [Fact]
    public void ProcessMonitor_CanStartAndDisposeWithFilteredOrdinaryUserEnumeration()
    {
        using PollingProtectedGameProcessMonitor monitor = new(
            ["codex-genshin-monitor-qa-impossible.exe"]);

        monitor.Start();

        Assert.False(monitor.IsRunning);
    }
}
