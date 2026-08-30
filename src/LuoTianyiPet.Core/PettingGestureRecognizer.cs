namespace LuoTianyiPet.Core;

public enum PettingGestureAction
{
    None,
    Completed,
    YieldToWindowDrag,
}

public sealed class PettingGestureRecognizer
{
    private readonly TimeSpan _minimumDuration;
    private readonly double _minimumHorizontalTravel;
    private readonly int _minimumDirectionReversals;
    private readonly double _dragEscapeDistance;
    private PointerPoint _start;
    private PointerPoint _last;
    private DateTimeOffset _startedAt;
    private int _lastHorizontalDirection;
    private int _directionReversals;
    private double _horizontalTravel;

    public PettingGestureRecognizer(
        TimeSpan minimumDuration,
        double minimumHorizontalTravel,
        int minimumDirectionReversals,
        double dragEscapeDistance)
    {
        if (minimumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDuration));
        }

        if (minimumHorizontalTravel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumHorizontalTravel));
        }

        if (minimumDirectionReversals < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDirectionReversals));
        }

        if (dragEscapeDistance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dragEscapeDistance));
        }

        _minimumDuration = minimumDuration;
        _minimumHorizontalTravel = minimumHorizontalTravel;
        _minimumDirectionReversals = minimumDirectionReversals;
        _dragEscapeDistance = dragEscapeDistance;
    }

    public bool IsTracking { get; private set; }

    public void Begin(PointerPoint start, DateTimeOffset now)
    {
        _start = start;
        _last = start;
        _startedAt = now;
        _lastHorizontalDirection = 0;
        _directionReversals = 0;
        _horizontalTravel = 0;
        IsTracking = true;
    }

    public PettingGestureAction Move(PointerPoint point, DateTimeOffset now)
    {
        if (!IsTracking)
        {
            return PettingGestureAction.None;
        }

        double displacementX = point.X - _start.X;
        double displacementY = point.Y - _start.Y;
        if (Math.Sqrt(displacementX * displacementX + displacementY * displacementY) >= _dragEscapeDistance)
        {
            Cancel();
            return PettingGestureAction.YieldToWindowDrag;
        }

        double stepX = point.X - _last.X;
        _horizontalTravel += Math.Abs(stepX);
        if (Math.Abs(stepX) >= 2)
        {
            int direction = Math.Sign(stepX);
            if (_lastHorizontalDirection != 0 && direction != _lastHorizontalDirection)
            {
                _directionReversals++;
            }

            _lastHorizontalDirection = direction;
        }

        _last = point;
        if (now - _startedAt >= _minimumDuration &&
            _horizontalTravel >= _minimumHorizontalTravel &&
            _directionReversals >= _minimumDirectionReversals)
        {
            Cancel();
            return PettingGestureAction.Completed;
        }

        return PettingGestureAction.None;
    }

    public void Cancel()
    {
        IsTracking = false;
        _lastHorizontalDirection = 0;
        _directionReversals = 0;
        _horizontalTravel = 0;
    }
}
