using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class WindowsMediaApplicationLauncherTests
{
    [Fact]
    public void TryLaunch_AlreadyRunning_DoesNotResolveOrStartExecutable()
    {
        bool resolved = false;
        bool started = false;
        WindowsMediaApplicationLauncher launcher = CreateLauncher(
            isRunning: _ => true,
            resolveExecutable: _ =>
            {
                resolved = true;
                return "C:\\CloudMusic\\cloudmusic.exe";
            },
            startExecutable: _ =>
            {
                started = true;
                return true;
            });

        MediaApplicationLaunchResult result = launcher.TryLaunch("cloudmusic.exe");

        Assert.Equal(MediaApplicationLaunchStatus.AlreadyRunning, result.Status);
        Assert.False(resolved);
        Assert.False(started);
    }

    [Fact]
    public void TryLaunch_ProtectedApplicationForeground_FailsClosed()
    {
        FakeShortcutInputBackend backend = new()
        {
            Foreground = new(true, "YuanShen.exe"),
        };
        bool started = false;
        WindowsMediaApplicationLauncher launcher = CreateLauncher(
            backend,
            startExecutable: _ =>
            {
                started = true;
                return true;
            });

        MediaApplicationLaunchResult result = launcher.TryLaunch("cloudmusic.exe");

        Assert.Equal(MediaApplicationLaunchStatus.ProtectedApplicationForeground, result.Status);
        Assert.False(started);
    }

    [Fact]
    public void TryLaunch_ForegroundLookupFails_FailsClosed()
    {
        FakeShortcutInputBackend backend = new()
        {
            Foreground = new(false, null),
        };
        WindowsMediaApplicationLauncher launcher = CreateLauncher(backend);

        MediaApplicationLaunchResult result = launcher.TryLaunch("cloudmusic.exe");

        Assert.Equal(MediaApplicationLaunchStatus.ForegroundCheckUnavailable, result.Status);
    }

    [Fact]
    public void TryLaunch_ExecutableCannotBeResolved_ReportsNotFound()
    {
        WindowsMediaApplicationLauncher launcher = CreateLauncher(resolveExecutable: _ => null);

        MediaApplicationLaunchResult result = launcher.TryLaunch("cloudmusic.exe");

        Assert.Equal(MediaApplicationLaunchStatus.NotFound, result.Status);
    }

    [Fact]
    public void TryLaunch_ProcessLookupThrows_FailsWithoutStarting()
    {
        bool started = false;
        WindowsMediaApplicationLauncher launcher = CreateLauncher(
            isRunning: _ => throw new InvalidOperationException(),
            startExecutable: _ =>
            {
                started = true;
                return true;
            });

        MediaApplicationLaunchResult result = launcher.TryLaunch("cloudmusic.exe");

        Assert.Equal(MediaApplicationLaunchStatus.SystemRejected, result.Status);
        Assert.False(started);
    }

    [Fact]
    public void TryLaunch_ExecutableResolutionThrows_FailsWithoutStarting()
    {
        bool started = false;
        WindowsMediaApplicationLauncher launcher = CreateLauncher(
            resolveExecutable: _ => throw new IOException(),
            startExecutable: _ =>
            {
                started = true;
                return true;
            });

        MediaApplicationLaunchResult result = launcher.TryLaunch("cloudmusic.exe");

        Assert.Equal(MediaApplicationLaunchStatus.SystemRejected, result.Status);
        Assert.False(started);
    }

    [Fact]
    public void TryLaunch_ResolvedExecutable_StartsOnce()
    {
        string? startedPath = null;
        WindowsMediaApplicationLauncher launcher = CreateLauncher(
            startExecutable: path =>
            {
                startedPath = path;
                return true;
            });

        MediaApplicationLaunchResult result = launcher.TryLaunch("cloudmusic.exe");

        Assert.Equal(MediaApplicationLaunchStatus.Started, result.Status);
        Assert.Equal("C:\\CloudMusic\\cloudmusic.exe", startedPath);
    }

    private static WindowsMediaApplicationLauncher CreateLauncher(
        FakeShortcutInputBackend? backend = null,
        Func<string, bool>? isRunning = null,
        Func<string, string?>? resolveExecutable = null,
        Func<string, bool>? startExecutable = null) =>
        new(
            backend ?? new FakeShortcutInputBackend(),
            new SafetyPreferences(),
            isRunning ?? (_ => false),
            resolveExecutable ?? (_ => "C:\\CloudMusic\\cloudmusic.exe"),
            startExecutable ?? (_ => true));

    private sealed class FakeShortcutInputBackend : IShortcutInputBackend
    {
        public ForegroundProcessQuery Foreground { get; init; } = new(true, "explorer.exe");

        public ForegroundProcessQuery QueryForegroundProcess() => Foreground;

        public bool IsKeyDown(ushort virtualKey) => false;

        public int Send(IReadOnlyList<ShortcutKeyStroke> strokes) => strokes.Count;
    }
}
