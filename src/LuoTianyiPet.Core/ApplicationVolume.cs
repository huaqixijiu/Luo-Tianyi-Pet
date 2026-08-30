namespace LuoTianyiPet.Core;

public readonly record struct ApplicationVolumeSnapshot(
    bool ProbeSucceeded,
    bool TargetSessionFound,
    float Level)
{
    public static ApplicationVolumeSnapshot Unavailable { get; } = new(false, false, 0);

    public static ApplicationVolumeSnapshot Missing { get; } = new(true, false, 0);

    public bool IsAvailable => ProbeSucceeded && TargetSessionFound;

    public int Percentage => (int)Math.Round(
        Math.Clamp(Level, 0, 1) * 100,
        MidpointRounding.AwayFromZero);

    public static ApplicationVolumeSnapshot Found(float level)
    {
        if (!float.IsFinite(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        return new(true, true, Math.Clamp(level, 0, 1));
    }
}

public enum ApplicationVolumeAdjustmentStatus
{
    Succeeded,
    AtLimit,
    TargetSessionMissing,
    SessionUnavailable,
    ProtectedApplicationForeground,
    ForegroundCheckUnavailable,
    SystemRejected,
}

public sealed record ApplicationVolumeAdjustmentResult(
    ApplicationVolumeAdjustmentStatus Status,
    ApplicationVolumeSnapshot Snapshot);

public interface IApplicationVolumeService : IDisposable
{
    ApplicationVolumeSnapshot Read();

    ApplicationVolumeAdjustmentResult TrySetLevel(float level);
}
