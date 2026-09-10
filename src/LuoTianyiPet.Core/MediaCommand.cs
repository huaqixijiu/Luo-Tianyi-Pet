namespace LuoTianyiPet.Core;

public enum MediaCommand
{
    PreviousTrack,
    TogglePlayPause,
    NextTrack,
}

public enum MediaCommandDeliveryMethod
{
    None,
    TargetedWindowsMessage,
    KeyboardShortcut,
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

public sealed record MediaCommandSendResult(
    MediaCommandSendStatus Status,
    MediaCommandDeliveryMethod DeliveryMethod = MediaCommandDeliveryMethod.None)
{
    public bool WasSent => Status == MediaCommandSendStatus.Sent;

    public bool WasSentViaTargetedMessage =>
        WasSent && DeliveryMethod == MediaCommandDeliveryMethod.TargetedWindowsMessage;
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
