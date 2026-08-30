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
