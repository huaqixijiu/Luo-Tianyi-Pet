namespace LuoTianyiPet.Core;

public sealed class DownwardFlingTracker
{
    private readonly double _minimumTotalDrop;
    private readonly double _minimumRecentDrop;
    private readonly double _minimumRecentVelocity;
    private readonly TimeSpan _recentWindow;
    private readonly List<MotionSample> _samples = [];
    private MotionSample? _origin;

    public DownwardFlingTracker(
        double minimumTotalDrop = 72,
        double minimumRecentDrop = 22,
        double minimumRecentVelocity = 650,
        TimeSpan? recentWindow = null)
    {
        _recentWindow = recentWindow ?? TimeSpan.FromMilliseconds(180);
        if (!double.IsFinite(minimumTotalDrop) || minimumTotalDrop <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTotalDrop));
        }

        if (!double.IsFinite(minimumRecentDrop) || minimumRecentDrop <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumRecentDrop));
        }

        if (!double.IsFinite(minimumRecentVelocity) || minimumRecentVelocity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumRecentVelocity));
        }

        if (_recentWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(recentWindow));
        }

        _minimumTotalDrop = minimumTotalDrop;
        _minimumRecentDrop = minimumRecentDrop;
        _minimumRecentVelocity = minimumRecentVelocity;
    }

    public void Begin(PointerPoint position, DateTimeOffset observedAt)
    {
        Validate(position);
        MotionSample sample = new(position, observedAt);
        _origin = sample;
        _samples.Clear();
        _samples.Add(sample);
    }

    public void Add(PointerPoint position, DateTimeOffset observedAt)
    {
        Validate(position);
        if (_origin is null)
        {
            return;
        }

        if (_samples.Count > 0 && observedAt < _samples[^1].ObservedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(observedAt));
        }

        _samples.Add(new MotionSample(position, observedAt));
        DateTimeOffset retentionStart = observedAt - _recentWindow - _recentWindow;
        _samples.RemoveAll(sample => sample.ObservedAt < retentionStart && sample != _origin);
    }

    public bool Complete(PointerPoint releasePosition, DateTimeOffset observedAt)
    {
        Validate(releasePosition);
        if (_origin is not MotionSample origin)
        {
            return false;
        }

        Add(releasePosition, observedAt);
        DateTimeOffset windowStart = observedAt - _recentWindow;
        MotionSample recentAnchor = _samples.First(sample => sample.ObservedAt >= windowStart);
        double elapsedSeconds = (observedAt - recentAnchor.ObservedAt).TotalSeconds;
        double totalDrop = releasePosition.Y - origin.Position.Y;
        double recentDrop = releasePosition.Y - recentAnchor.Position.Y;
        double recentVelocity = elapsedSeconds > 0
            ? recentDrop / elapsedSeconds
            : 0;

        Cancel();
        return totalDrop >= _minimumTotalDrop &&
            recentDrop >= _minimumRecentDrop &&
            recentVelocity >= _minimumRecentVelocity;
    }

    public void Cancel()
    {
        _origin = null;
        _samples.Clear();
    }

    private static void Validate(PointerPoint position)
    {
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
    }

    private sealed record MotionSample(PointerPoint Position, DateTimeOffset ObservedAt);
}
