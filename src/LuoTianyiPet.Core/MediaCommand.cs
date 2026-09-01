namespace LuoTianyiPet.Core;

public enum MediaCommand
{
    PreviousTrack,
    TogglePlayPause,
    NextTrack,
}

public enum MediaCommandSendStatus
{
    Sent,
    Disabled,
    InvalidShortcut,
    ProtectedApplicationForeground,
    ForegroundCheckUnavailable,
    KeyboardBusy,
    RateLimited,
    SystemRejected,
}

public sealed record MediaCommandSendResult(MediaCommandSendStatus Status)
{
    public bool WasSent => Status == MediaCommandSendStatus.Sent;
}

public interface IMediaCommandSender
{
    MediaCommandSendResult TrySend(MediaCommand command, DateTimeOffset now);
}

public enum MediaApplicationLaunchStatus
{
    AlreadyRunning,
    Started,
    NotFound,
    ProtectedApplicationForeground,
    ForegroundCheckUnavailable,
    SystemRejected,
}

public sealed record MediaApplicationLaunchResult(MediaApplicationLaunchStatus Status)
{
    public bool IsReadyOrStarted =>
        Status is MediaApplicationLaunchStatus.AlreadyRunning or MediaApplicationLaunchStatus.Started;
}

public interface IMediaApplicationLauncher
{
    bool IsRunning(string processName);

    MediaApplicationLaunchResult TryLaunch(string processName);
}
