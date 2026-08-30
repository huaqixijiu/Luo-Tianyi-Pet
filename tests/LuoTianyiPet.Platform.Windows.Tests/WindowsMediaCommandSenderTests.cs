using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class WindowsMediaCommandSenderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TrySend_DefaultPreviousTrack_SendsBalancedShortcutSequence()
    {
        FakeShortcutInputBackend backend = new();
        WindowsMediaCommandSender sender = CreateSender(backend);

        MediaCommandSendResult result = sender.TrySend(MediaCommand.PreviousTrack, Now);

        Assert.Equal(MediaCommandSendStatus.Sent, result.Status);
        Assert.Equal(
            [
                new ShortcutKeyStroke(0x11, false),
                new ShortcutKeyStroke(0x12, false),
                new ShortcutKeyStroke(0x25, false),
                new ShortcutKeyStroke(0x25, true),
                new ShortcutKeyStroke(0x12, true),
                new ShortcutKeyStroke(0x11, true),
            ],
            backend.LastStrokes);
    }

    [Fact]
    public void TrySend_DefaultFavoriteTrack_UsesCloudMusicDefaultShortcut()
    {
        FakeShortcutInputBackend backend = new();
        WindowsMediaCommandSender sender = CreateSender(backend);

        MediaCommandSendResult result = sender.TrySend(MediaCommand.FavoriteTrack, Now);

        Assert.Equal(MediaCommandSendStatus.Sent, result.Status);
        Assert.Contains(new ShortcutKeyStroke(0x4C, false), backend.LastStrokes!);
        Assert.Contains(new ShortcutKeyStroke(0x4C, true), backend.LastStrokes!);
    }

    [Theory]
    [InlineData("YuanShen")]
    [InlineData("genshinimpact.exe")]
    public void TrySend_ProtectedApplicationForeground_RejectsWithoutInput(string processName)
    {
        FakeShortcutInputBackend backend = new()
        {
            Foreground = new(true, processName),
        };
        WindowsMediaCommandSender sender = CreateSender(backend);

        MediaCommandSendResult result = sender.TrySend(MediaCommand.TogglePlayPause, Now);

        Assert.Equal(MediaCommandSendStatus.ProtectedApplicationForeground, result.Status);
        Assert.Null(backend.LastStrokes);
    }

    [Fact]
    public void TrySend_ForegroundLookupFails_FailsClosed()
    {
        FakeShortcutInputBackend backend = new()
        {
            Foreground = new(false, null),
        };
        WindowsMediaCommandSender sender = CreateSender(backend);

        MediaCommandSendResult result = sender.TrySend(MediaCommand.NextTrack, Now);

        Assert.Equal(MediaCommandSendStatus.ForegroundCheckUnavailable, result.Status);
        Assert.Null(backend.LastStrokes);
    }

    [Theory]
    [InlineData(0x11)]
    [InlineData(0x5B)]
    [InlineData(0x50)]
    public void TrySend_UserAlreadyHoldsRelevantKey_RejectsWithoutChangingKeyState(ushort key)
    {
        FakeShortcutInputBackend backend = new();
        backend.DownKeys.Add(key);
        WindowsMediaCommandSender sender = CreateSender(backend);

        MediaCommandSendResult result = sender.TrySend(MediaCommand.TogglePlayPause, Now);

        Assert.Equal(MediaCommandSendStatus.KeyboardBusy, result.Status);
        Assert.Null(backend.LastStrokes);
    }

    [Fact]
    public void TrySend_WithinCooldown_RejectsSecondCommand()
    {
        FakeShortcutInputBackend backend = new();
        WindowsMediaCommandSender sender = CreateSender(backend);

        MediaCommandSendResult first = sender.TrySend(MediaCommand.NextTrack, Now);
        backend.LastStrokes = null;
        MediaCommandSendResult second = sender.TrySend(MediaCommand.NextTrack, Now.AddMilliseconds(349));

        Assert.True(first.WasSent);
        Assert.Equal(MediaCommandSendStatus.RateLimited, second.Status);
        Assert.Null(backend.LastStrokes);
    }

    [Fact]
    public void TrySend_AfterCooldown_AllowsNextCommand()
    {
        FakeShortcutInputBackend backend = new();
        WindowsMediaCommandSender sender = CreateSender(backend);

        sender.TrySend(MediaCommand.NextTrack, Now);
        MediaCommandSendResult result = sender.TrySend(MediaCommand.PreviousTrack, Now.AddMilliseconds(350));

        Assert.Equal(MediaCommandSendStatus.Sent, result.Status);
    }

    [Fact]
    public void TrySend_InvalidShortcut_RejectsWithoutInput()
    {
        FakeShortcutInputBackend backend = new();
        MediaPreferences media = new()
        {
            NextTrackShortcut = "Right",
        };
        WindowsMediaCommandSender sender = new(backend, media, new SafetyPreferences());

        MediaCommandSendResult result = sender.TrySend(MediaCommand.NextTrack, Now);

        Assert.Equal(MediaCommandSendStatus.InvalidShortcut, result.Status);
        Assert.Null(backend.LastStrokes);
    }

    [Fact]
    public void TrySend_Disabled_RejectsBeforeForegroundLookup()
    {
        FakeShortcutInputBackend backend = new();
        MediaPreferences media = new()
        {
            EnableCloudMusicShortcutControl = false,
        };
        WindowsMediaCommandSender sender = new(backend, media, new SafetyPreferences());

        MediaCommandSendResult result = sender.TrySend(MediaCommand.NextTrack, Now);

        Assert.Equal(MediaCommandSendStatus.Disabled, result.Status);
        Assert.Equal(0, backend.ForegroundQueryCount);
    }

    [Fact]
    public void TrySend_PartialSystemAcceptance_ReportsFailureAndDoesNotStartCooldown()
    {
        FakeShortcutInputBackend backend = new()
        {
            AcceptedStrokeCount = 1,
        };
        WindowsMediaCommandSender sender = CreateSender(backend);

        MediaCommandSendResult failed = sender.TrySend(MediaCommand.NextTrack, Now);
        Assert.Equal(2, backend.SendCallCount);
        Assert.All(backend.LastStrokes!, stroke => Assert.True(stroke.IsKeyUp));
        backend.AcceptedStrokeCount = null;
        MediaCommandSendResult retried = sender.TrySend(MediaCommand.NextTrack, Now.AddMilliseconds(1));

        Assert.Equal(MediaCommandSendStatus.SystemRejected, failed.Status);
        Assert.Equal(MediaCommandSendStatus.Sent, retried.Status);
    }

    private static WindowsMediaCommandSender CreateSender(FakeShortcutInputBackend backend) =>
        new(backend, new MediaPreferences(), new SafetyPreferences());

    private sealed class FakeShortcutInputBackend : IShortcutInputBackend
    {
        public ForegroundProcessQuery Foreground { get; init; } = new(true, "explorer");

        public HashSet<ushort> DownKeys { get; } = [];

        public IReadOnlyList<ShortcutKeyStroke>? LastStrokes { get; set; }

        public int? AcceptedStrokeCount { get; set; }

        public int ForegroundQueryCount { get; private set; }

        public int SendCallCount { get; private set; }

        public ForegroundProcessQuery QueryForegroundProcess()
        {
            ForegroundQueryCount++;
            return Foreground;
        }

        public bool IsKeyDown(ushort virtualKey) => DownKeys.Contains(virtualKey);

        public int Send(IReadOnlyList<ShortcutKeyStroke> strokes)
        {
            SendCallCount++;
            LastStrokes = strokes;
            return AcceptedStrokeCount ?? strokes.Count;
        }
    }
}
