namespace LuoTianyiPet.Core;

public readonly record struct SystemVolumeSnapshot(bool IsAvailable, float Level, bool IsMuted)
{
    public static SystemVolumeSnapshot Unavailable { get; } = new(false, 0, false);

    public int Percentage => (int)Math.Round(
        Math.Clamp(Level, 0, 1) * 100,
        MidpointRounding.AwayFromZero);

    public static SystemVolumeSnapshot Available(float level, bool isMuted)
    {
        if (!float.IsFinite(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        return new(true, Math.Clamp(level, 0, 1), isMuted);
    }
}

public sealed class SystemVolumeChangedEventArgs(SystemVolumeSnapshot snapshot) : EventArgs
{
    public SystemVolumeSnapshot Snapshot { get; } = snapshot;
}

public enum SystemVolumeChangeKind
{
    None,
    Increased,
    Decreased,
    Muted,
    Unmuted,
}

public readonly record struct SystemVolumeFeedbackDecision(
    SystemVolumeChangeKind Kind,
    SystemVolumeSnapshot Snapshot)
{
    public bool ShouldShow => Kind != SystemVolumeChangeKind.None;

    public bool ShouldAnimate => Kind is SystemVolumeChangeKind.Increased or SystemVolumeChangeKind.Decreased;
}

public sealed class SystemVolumeChangeTracker(float levelEpsilon = 0.0005f)
{
    private readonly float _levelEpsilon = levelEpsilon >= 0 && float.IsFinite(levelEpsilon)
        ? levelEpsilon
        : throw new ArgumentOutOfRangeException(nameof(levelEpsilon));
    private SystemVolumeSnapshot _previous;
    private bool _hasPrevious;

    public SystemVolumeFeedbackDecision Observe(SystemVolumeSnapshot snapshot)
    {
        if (!snapshot.IsAvailable)
        {
            return new(SystemVolumeChangeKind.None, snapshot);
        }

        if (!_hasPrevious)
        {
            _previous = snapshot;
            _hasPrevious = true;
            return new(SystemVolumeChangeKind.None, snapshot);
        }

        SystemVolumeSnapshot previous = _previous;
        _previous = snapshot;
        float difference = snapshot.Level - previous.Level;
        SystemVolumeChangeKind kind = difference switch
        {
            > 0 when difference > _levelEpsilon => SystemVolumeChangeKind.Increased,
            < 0 when -difference > _levelEpsilon => SystemVolumeChangeKind.Decreased,
            _ when snapshot.IsMuted != previous.IsMuted => snapshot.IsMuted
                ? SystemVolumeChangeKind.Muted
                : SystemVolumeChangeKind.Unmuted,
            _ => SystemVolumeChangeKind.None,
        };
        return new(kind, snapshot);
    }
}

public enum SystemVolumeSafetyStatus
{
    Allowed,
    ProtectedApplicationForeground,
    ForegroundCheckUnavailable,
}

public enum SystemVolumeAdjustmentStatus
{
    Succeeded,
    AtLimit,
    Disabled,
    ProtectedApplicationForeground,
    ForegroundCheckUnavailable,
    EndpointUnavailable,
    SystemRejected,
}

public sealed record SystemVolumeAdjustmentResult(
    SystemVolumeAdjustmentStatus Status,
    SystemVolumeSnapshot Snapshot)
{
    public bool WasAdjusted => Status == SystemVolumeAdjustmentStatus.Succeeded;
}

public interface ISystemVolumeService : IDisposable
{
    event EventHandler<SystemVolumeChangedEventArgs>? VolumeChanged;

    SystemVolumeSnapshot Read();

    SystemVolumeSafetyStatus CheckFeedbackSafety();

    SystemVolumeAdjustmentResult TryAdjustBySteps(int steps);

    SystemVolumeAdjustmentResult TrySetLevel(float level);

    void UpdatePreferences(VolumePreferences preferences);
}
