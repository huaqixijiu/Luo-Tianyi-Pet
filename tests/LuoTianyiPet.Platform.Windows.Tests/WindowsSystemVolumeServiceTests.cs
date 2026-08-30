using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class WindowsSystemVolumeServiceTests
{
    [Fact]
    public void TryAdjustBySteps_IncreasesUsingConfiguredStep()
    {
        FakeBackend backend = new()
        {
            Snapshot = SystemVolumeSnapshot.Available(0.4f, false),
        };
        using WindowsSystemVolumeService service = Create(backend);

        SystemVolumeAdjustmentResult result = service.TryAdjustBySteps(1);

        Assert.Equal(SystemVolumeAdjustmentStatus.Succeeded, result.Status);
        Assert.Equal(0.42f, result.Snapshot.Level, 3);
        Assert.Equal(0.42f, backend.LastSetLevel, 3);
    }

    [Fact]
    public void TryAdjustBySteps_ClampsAndReportsLimit()
    {
        FakeBackend backend = new()
        {
            Snapshot = SystemVolumeSnapshot.Available(0.99f, false),
        };
        using WindowsSystemVolumeService service = Create(backend);

        Assert.Equal(SystemVolumeAdjustmentStatus.Succeeded, service.TryAdjustBySteps(1).Status);
        Assert.Equal(1f, backend.LastSetLevel, 3);
        Assert.Equal(SystemVolumeAdjustmentStatus.AtLimit, service.TryAdjustBySteps(1).Status);
        Assert.Equal(1, backend.SetCount);
    }

    [Theory]
    [InlineData(false, "YuanShen", SystemVolumeAdjustmentStatus.ProtectedApplicationForeground)]
    [InlineData(false, "GenshinImpact.exe", SystemVolumeAdjustmentStatus.ProtectedApplicationForeground)]
    [InlineData(true, null, SystemVolumeAdjustmentStatus.ForegroundCheckUnavailable)]
    public void TryAdjustBySteps_UnsafeForeground_FailsClosed(
        bool queryFailed,
        string? processName,
        SystemVolumeAdjustmentStatus expected)
    {
        FakeBackend backend = new()
        {
            Foreground = new(!queryFailed, processName),
            Snapshot = SystemVolumeSnapshot.Available(0.4f, false),
        };
        using WindowsSystemVolumeService service = Create(backend);

        SystemVolumeAdjustmentResult result = service.TryAdjustBySteps(1);

        Assert.Equal(expected, result.Status);
        Assert.Equal(0, backend.ReadCount);
        Assert.Equal(0, backend.SetCount);
    }

    [Fact]
    public void TryAdjustBySteps_Disabled_DoesNotQueryOrSet()
    {
        FakeBackend backend = new();
        using WindowsSystemVolumeService service = Create(
            backend,
            new VolumePreferences { EnableMouseWheelControl = false });

        SystemVolumeAdjustmentResult result = service.TryAdjustBySteps(1);

        Assert.Equal(SystemVolumeAdjustmentStatus.Disabled, result.Status);
        Assert.Equal(0, backend.ForegroundQueryCount);
        Assert.Equal(0, backend.SetCount);
    }

    [Fact]
    public void TryAdjustBySteps_UnavailableEndpoint_ReturnsWithoutSet()
    {
        FakeBackend backend = new() { Snapshot = SystemVolumeSnapshot.Unavailable };
        using WindowsSystemVolumeService service = Create(backend);

        SystemVolumeAdjustmentResult result = service.TryAdjustBySteps(1);

        Assert.Equal(SystemVolumeAdjustmentStatus.EndpointUnavailable, result.Status);
        Assert.Equal(0, backend.SetCount);
    }

    [Fact]
    public void TryAdjustBySteps_BackendRejectsWrite_ReturnsSystemRejected()
    {
        FakeBackend backend = new() { SetSucceeds = false };
        using WindowsSystemVolumeService service = Create(backend);

        SystemVolumeAdjustmentResult result = service.TryAdjustBySteps(1);

        Assert.Equal(SystemVolumeAdjustmentStatus.SystemRejected, result.Status);
        Assert.Equal(1, backend.SetCount);
    }

    [Fact]
    public void TrySetLevel_WorksWhenMouseWheelControlIsDisabled()
    {
        FakeBackend backend = new();
        using WindowsSystemVolumeService service = Create(
            backend,
            new VolumePreferences { EnableMouseWheelControl = false });

        SystemVolumeAdjustmentResult result = service.TrySetLevel(0.73f);

        Assert.Equal(SystemVolumeAdjustmentStatus.Succeeded, result.Status);
        Assert.Equal(0.73f, backend.LastSetLevel, 3);
    }

    [Fact]
    public void UpdatePreferences_AppliesWheelToggleAndStepWithoutRestart()
    {
        FakeBackend backend = new();
        using WindowsSystemVolumeService service = Create(backend);

        service.UpdatePreferences(new VolumePreferences
        {
            EnableMouseWheelControl = false,
            MouseWheelStepPercent = 5,
        });
        Assert.Equal(SystemVolumeAdjustmentStatus.Disabled, service.TryAdjustBySteps(1).Status);

        service.UpdatePreferences(new VolumePreferences
        {
            EnableMouseWheelControl = true,
            MouseWheelStepPercent = 5,
        });
        SystemVolumeAdjustmentResult result = service.TryAdjustBySteps(1);

        Assert.Equal(SystemVolumeAdjustmentStatus.Succeeded, result.Status);
        Assert.Equal(0.55f, backend.LastSetLevel, 3);
    }

    [Fact]
    public void TrySetLevel_ProtectedForeground_DoesNotWrite()
    {
        FakeBackend backend = new()
        {
            Foreground = new(true, "YuanShen.exe"),
        };
        using WindowsSystemVolumeService service = Create(backend);

        SystemVolumeAdjustmentResult result = service.TrySetLevel(0.8f);

        Assert.Equal(SystemVolumeAdjustmentStatus.ProtectedApplicationForeground, result.Status);
        Assert.Equal(0, backend.SetCount);
    }

    [Fact]
    public void VolumeChanged_ForwardsBackendNotificationUntilDisposed()
    {
        FakeBackend backend = new();
        WindowsSystemVolumeService service = Create(backend);
        List<SystemVolumeSnapshot> received = [];
        service.VolumeChanged += (_, eventArgs) => received.Add(eventArgs.Snapshot);

        backend.Raise(SystemVolumeSnapshot.Available(0.6f, true));
        service.Dispose();
        backend.Raise(SystemVolumeSnapshot.Available(0.7f, false));

        Assert.Single(received);
        Assert.Equal(60, received[0].Percentage);
        Assert.True(received[0].IsMuted);
        Assert.True(backend.Disposed);
    }

    private static WindowsSystemVolumeService Create(
        FakeBackend backend,
        VolumePreferences? preferences = null) =>
        new(
            backend,
            preferences ?? new VolumePreferences(),
            new SafetyPreferences());

    private sealed class FakeBackend : ISystemVolumeBackend
    {
        public event EventHandler<SystemVolumeChangedEventArgs>? VolumeChanged;

        public ForegroundProcessQuery Foreground { get; init; } = new(true, "explorer");

        public SystemVolumeSnapshot Snapshot { get; set; } =
            SystemVolumeSnapshot.Available(0.5f, false);

        public int ForegroundQueryCount { get; private set; }

        public int ReadCount { get; private set; }

        public int SetCount { get; private set; }

        public float LastSetLevel { get; private set; }

        public bool Disposed { get; private set; }

        public bool SetSucceeds { get; init; } = true;

        public ForegroundProcessQuery QueryForegroundProcess()
        {
            ForegroundQueryCount++;
            return Foreground;
        }

        public SystemVolumeSnapshot Read()
        {
            ReadCount++;
            return Snapshot;
        }

        public bool TrySetLevel(float level, out SystemVolumeSnapshot snapshot)
        {
            SetCount++;
            LastSetLevel = level;
            if (!SetSucceeds)
            {
                snapshot = SystemVolumeSnapshot.Unavailable;
                return false;
            }

            Snapshot = SystemVolumeSnapshot.Available(level, Snapshot.IsMuted);
            snapshot = Snapshot;
            return true;
        }

        public void Raise(SystemVolumeSnapshot snapshot) =>
            VolumeChanged?.Invoke(this, new SystemVolumeChangedEventArgs(snapshot));

        public void Dispose() => Disposed = true;
    }
}
