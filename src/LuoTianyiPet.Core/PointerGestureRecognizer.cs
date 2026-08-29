namespace LuoTianyiPet.Core;

public readonly record struct PointerPoint(double X, double Y);

public enum PointerGestureActionType
{
    None,
    DispatchSingleClick,
    ToggleDisplayMode,
    BeginDrag,
    EndDrag,
}

public readonly record struct PointerGestureAction(
    PointerGestureActionType Type,
    PointerPoint? Position = null)
{
    public static PointerGestureAction None => new(PointerGestureActionType.None);
}

public sealed class PointerGestureRecognizer
{
    private readonly double _dragThreshold;
    private readonly TimeSpan _doubleClickInterval;
    private PendingSingleClick? _pendingSingleClick;
    private PointerPoint _pressPosition;
    private bool _isPressed;
    private bool _isDragging;
    private bool _suppressCurrentRelease;

    public PointerGestureRecognizer(double dragThreshold, TimeSpan doubleClickInterval)
    {
        if (!double.IsFinite(dragThreshold) || dragThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dragThreshold));
        }

        if (doubleClickInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(doubleClickInterval));
        }

        _dragThreshold = dragThreshold;
        _doubleClickInterval = doubleClickInterval;
    }

    public bool IsDragging => _isDragging;

    public bool HasPendingSingleClick => _pendingSingleClick is not null;

    public TimeSpan? TimeUntilPendingSingleClick(DateTimeOffset now)
    {
        if (_pendingSingleClick is null)
        {
            return null;
        }

        TimeSpan remaining = _pendingSingleClick.DueAt - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public PointerGestureAction Press(PointerPoint position, int clickCount, DateTimeOffset now)
    {
        ValidatePoint(position);
        if (clickCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clickCount));
        }

        _isPressed = true;
        _isDragging = false;
        _suppressCurrentRelease = false;
        _pressPosition = position;

        if (_pendingSingleClick is null)
        {
            return PointerGestureAction.None;
        }

        PendingSingleClick pending = _pendingSingleClick;
        bool isEligibleDoubleClick = clickCount >= 2 &&
            now <= pending.DueAt &&
            Distance(position, pending.Position) < _dragThreshold;
        _pendingSingleClick = null;

        if (isEligibleDoubleClick)
        {
            _suppressCurrentRelease = true;
            return new PointerGestureAction(PointerGestureActionType.ToggleDisplayMode, position);
        }

        return new PointerGestureAction(PointerGestureActionType.DispatchSingleClick, pending.Position);
    }

    public PointerGestureAction Move(PointerPoint position)
    {
        ValidatePoint(position);
        if (!_isPressed || _isDragging || _suppressCurrentRelease)
        {
            return PointerGestureAction.None;
        }

        if (Distance(position, _pressPosition) < _dragThreshold)
        {
            return PointerGestureAction.None;
        }

        _pendingSingleClick = null;
        _isDragging = true;
        return new PointerGestureAction(PointerGestureActionType.BeginDrag, _pressPosition);
    }

    public PointerGestureAction Release(PointerPoint position, DateTimeOffset now)
    {
        ValidatePoint(position);
        if (!_isPressed)
        {
            return PointerGestureAction.None;
        }

        _isPressed = false;
        if (_suppressCurrentRelease)
        {
            _suppressCurrentRelease = false;
            return PointerGestureAction.None;
        }

        if (_isDragging)
        {
            _isDragging = false;
            return new PointerGestureAction(PointerGestureActionType.EndDrag, position);
        }

        _pendingSingleClick = new PendingSingleClick(position, now + _doubleClickInterval);
        return PointerGestureAction.None;
    }

    public PointerGestureAction FlushPendingSingleClick(DateTimeOffset now)
    {
        if (_pendingSingleClick is null || now < _pendingSingleClick.DueAt)
        {
            return PointerGestureAction.None;
        }

        PointerPoint position = _pendingSingleClick.Position;
        _pendingSingleClick = null;
        return new PointerGestureAction(PointerGestureActionType.DispatchSingleClick, position);
    }

    public void Cancel()
    {
        _pendingSingleClick = null;
        _isPressed = false;
        _isDragging = false;
        _suppressCurrentRelease = false;
    }

    private static double Distance(PointerPoint first, PointerPoint second)
    {
        double deltaX = first.X - second.X;
        double deltaY = first.Y - second.Y;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private static void ValidatePoint(PointerPoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }
    }

    private sealed record PendingSingleClick(PointerPoint Position, DateTimeOffset DueAt);
}
