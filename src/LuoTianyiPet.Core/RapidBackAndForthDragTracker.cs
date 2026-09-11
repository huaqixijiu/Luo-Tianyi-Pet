namespace LuoTianyiPet.Core;

public sealed class RapidBackAndForthDragTracker
{
    private readonly double _minimumLegDistance;
    private readonly double _minimumLegVelocity;
    private readonly int _requiredDirectionChanges;
    private readonly TimeSpan _maximumSequenceDuration;
    private MotionSample? _lastSample;
    private MotionSample? _legStart;
    private DateTimeOffset? _sequenceStartedAt;
    private int _direction;
    private int _qualifiedDirectionChanges;
    private bool _triggered;

    public RapidBackAndForthDragTracker(
        double minimumLegDistance = 32,
        double minimumLegVelocity = 360,
        int requiredDirectionChanges = 4,
        TimeSpan? maximumSequenceDuration = null)
    {
        if (!Numeric.IsFinite(minimumLegDistance) || minimumLegDistance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLegDistance));
        }

        if (!Numeric.IsFinite(minimumLegVelocity) || minimumLegVelocity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLegVelocity));
        }

        if (requiredDirectionChanges <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredDirectionChanges));
        }

        TimeSpan duration = maximumSequenceDuration ?? TimeSpan.FromMilliseconds(1400);
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSequenceDuration));
        }

        _minimumLegDistance = minimumLegDistance;
        _minimumLegVelocity = minimumLegVelocity;
        _requiredDirectionChanges = requiredDirectionChanges;
        _maximumSequenceDuration = duration;
    }

    public void Begin(PointerPoint position, DateTimeOffset observedAt)
    {
        Validate(position);
        MotionSample sample = new(position, observedAt);
        _lastSample = sample;
        _legStart = sample;
        _sequenceStartedAt = observedAt;
        _direction = 0;
        _qualifiedDirectionChanges = 0;
        _triggered = false;
    }

    public bool Add(PointerPoint position, DateTimeOffset observedAt)
    {
        Validate(position);
        if (_triggered || _lastSample is not MotionSample last ||
            _legStart is not MotionSample legStart ||
            _sequenceStartedAt is not DateTimeOffset sequenceStartedAt)
        {
            return false;
        }

        if (observedAt < last.ObservedAt)
        {
            Cancel();
            return false;
        }

        MotionSample sample = new(position, observedAt);
        if (observedAt - sequenceStartedAt > _maximumSequenceDuration)
        {
            Begin(position, observedAt);
            return false;
        }

        double incrementalX = position.X - last.Position.X;
        if (Math.Abs(incrementalX) < 2)
        {
            _lastSample = sample;
            return false;
        }

        int direction = Math.Sign(incrementalX);
        if (_direction == 0)
        {
            _direction = direction;
            _lastSample = sample;
            return false;
        }

        if (direction == _direction)
        {
            _lastSample = sample;
            return false;
        }

        double legDistance = Math.Abs(last.Position.X - legStart.Position.X);
        double legSeconds = (last.ObservedAt - legStart.ObservedAt).TotalSeconds;
        double legVelocity = legSeconds > 0 ? legDistance / legSeconds : 0;
        if (legDistance >= _minimumLegDistance && legVelocity >= _minimumLegVelocity)
        {
            _qualifiedDirectionChanges++;
        }
        else
        {
            _qualifiedDirectionChanges = 0;
            _sequenceStartedAt = last.ObservedAt;
        }

        _direction = direction;
        _legStart = last;
        _lastSample = sample;
        if (_qualifiedDirectionChanges < _requiredDirectionChanges)
        {
            return false;
        }

        _triggered = true;
        return true;
    }

    public void Cancel()
    {
        _lastSample = null;
        _legStart = null;
        _sequenceStartedAt = null;
        _direction = 0;
        _qualifiedDirectionChanges = 0;
        _triggered = false;
    }

    private static void Validate(PointerPoint position)
    {
        if (!Numeric.IsFinite(position.X) || !Numeric.IsFinite(position.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
    }

    private sealed record MotionSample(PointerPoint Position, DateTimeOffset ObservedAt);
}
