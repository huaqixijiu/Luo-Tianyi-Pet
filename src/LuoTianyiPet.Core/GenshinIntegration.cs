namespace LuoTianyiPet.Core;

public enum ProtectedGamePresenceTransition
{
    None,
    BecameRunning,
    BecameStopped,
}

public sealed class ProtectedGamePresenceChangedEventArgs(
    bool isRunning,
    DateTimeOffset occurredAt) : EventArgs
{
    public bool IsRunning { get; } = isRunning;

    public DateTimeOffset OccurredAt { get; } = occurredAt;
}

public interface IProtectedGameProcessMonitor : IDisposable
{
    event EventHandler<ProtectedGamePresenceChangedEventArgs>? PresenceChanged;

    bool IsRunning { get; }

    void Start();
}

public readonly record struct ForegroundApplicationSnapshot(
    bool Succeeded,
    string? ProcessName,
    bool IsFullscreen);

public interface IForegroundApplicationProbe
{
    ForegroundApplicationSnapshot Query();
}

public interface IWindowWorkAreaProvider
{
    DesktopRectangle GetForWindow(nint windowHandle);
}

public sealed class ProtectedGamePresenceTracker
{
    private readonly HashSet<string> _targetProcessNames;
    private readonly HashSet<uint> _processIds = [];
    private readonly object _sync = new();

    public ProtectedGamePresenceTracker(IEnumerable<string> targetProcessNames)
    {
        ArgumentNullException.ThrowIfNull(targetProcessNames);
        _targetProcessNames = targetProcessNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_targetProcessNames.Count == 0)
        {
            throw new ArgumentException("At least one protected game process name is required.", nameof(targetProcessNames));
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _processIds.Count > 0;
            }
        }
    }

    public bool IsTargetProcess(string? processName) =>
        !string.IsNullOrWhiteSpace(processName) &&
        _targetProcessNames.Contains(NormalizeProcessName(processName));

    public void Seed(string processName, uint processId)
    {
        if (processId == 0 || !IsTargetProcess(processName))
        {
            return;
        }

        lock (_sync)
        {
            _processIds.Add(processId);
        }
    }

    public ProtectedGamePresenceTransition ObserveStarted(string processName, uint processId)
    {
        if (processId == 0 || !IsTargetProcess(processName))
        {
            return ProtectedGamePresenceTransition.None;
        }

        lock (_sync)
        {
            bool wasRunning = _processIds.Count > 0;
            if (!_processIds.Add(processId))
            {
                return ProtectedGamePresenceTransition.None;
            }

            return wasRunning
                ? ProtectedGamePresenceTransition.None
                : ProtectedGamePresenceTransition.BecameRunning;
        }
    }

    public ProtectedGamePresenceTransition ObserveStopped(string processName, uint processId)
    {
        if (processId == 0 || !IsTargetProcess(processName))
        {
            return ProtectedGamePresenceTransition.None;
        }

        lock (_sync)
        {
            if (!_processIds.Remove(processId) || _processIds.Count > 0)
            {
                return ProtectedGamePresenceTransition.None;
            }

            return ProtectedGamePresenceTransition.BecameStopped;
        }
    }

    private static string NormalizeProcessName(string processName) =>
        Path.GetFileNameWithoutExtension(processName.Trim());
}

public enum GenshinCameoScheduleDecision
{
    None,
    Trigger,
}

public sealed class GenshinBackgroundCameoScheduler
{
    private static readonly TimeSpan WindowLength = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan MinimumCooldown = TimeSpan.FromMinutes(45);
    private readonly Func<int, int, int> _nextInclusiveMinute;
    private DateTimeOffset? _windowStart;
    private DateTimeOffset? _nextTrigger;
    private DateTimeOffset? _lastTrigger;
    private bool _wasEligible;
    private bool _postponeOnNextEligible;

    public GenshinBackgroundCameoScheduler(Func<int, int, int>? nextInclusiveMinute = null)
    {
        _nextInclusiveMinute = nextInclusiveMinute ??
            ((minimum, maximum) => Random.Shared.Next(minimum, maximum + 1));
    }

    public GenshinCameoScheduleDecision Update(
        DateTimeOffset now,
        bool gameIsRunning,
        bool canShow)
    {
        if (!gameIsRunning)
        {
            Reset();
            return GenshinCameoScheduleDecision.None;
        }

        if (!canShow)
        {
            if (_wasEligible && _nextTrigger is not null)
            {
                _postponeOnNextEligible = true;
            }

            _wasEligible = false;
            return GenshinCameoScheduleDecision.None;
        }

        if (!_wasEligible)
        {
            if (_nextTrigger is null)
            {
                StartWindow(now);
            }
            else if (_postponeOnNextEligible)
            {
                _windowStart = now;
                _nextTrigger = now.AddMinutes(NextMinute(5, 15));
                _postponeOnNextEligible = false;
            }

            _wasEligible = true;
        }

        if (_nextTrigger is not DateTimeOffset nextTrigger || now < nextTrigger)
        {
            return GenshinCameoScheduleDecision.None;
        }

        if (_lastTrigger is DateTimeOffset lastTrigger && now < lastTrigger + MinimumCooldown)
        {
            _nextTrigger = lastTrigger + MinimumCooldown;
            return GenshinCameoScheduleDecision.None;
        }

        _lastTrigger = now;
        ScheduleFollowingWindow(now);
        return GenshinCameoScheduleDecision.Trigger;
    }

    public void Reset()
    {
        _windowStart = null;
        _nextTrigger = null;
        _lastTrigger = null;
        _wasEligible = false;
        _postponeOnNextEligible = false;
    }

    private void StartWindow(DateTimeOffset now)
    {
        _windowStart = now;
        _nextTrigger = now.AddMinutes(NextMinute(5, 55));
    }

    private void ScheduleFollowingWindow(DateTimeOffset now)
    {
        DateTimeOffset nextWindow = (_windowStart ?? now) + WindowLength;
        while (nextWindow <= now)
        {
            nextWindow += WindowLength;
        }

        _windowStart = nextWindow;
        DateTimeOffset candidate = nextWindow.AddMinutes(NextMinute(5, 55));
        if (_lastTrigger is DateTimeOffset lastTrigger && candidate < lastTrigger + MinimumCooldown)
        {
            candidate = lastTrigger + MinimumCooldown;
        }

        _nextTrigger = candidate;
    }

    private int NextMinute(int minimum, int maximum)
    {
        int value = _nextInclusiveMinute(minimum, maximum);
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException($"Random minute must be between {minimum} and {maximum}.");
        }

        return value;
    }
}

public sealed class RandomPetPositionSelector(Func<double>? nextUnitValue = null)
{
    private readonly Func<double> _nextUnitValue = nextUnitValue ?? Random.Shared.NextDouble;

    public PointerPoint Select(
        DesktopRectangle workArea,
        double petWidth,
        double petHeight,
        double safeMargin)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0 ||
            petWidth <= 0 || petHeight <= 0 || safeMargin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea));
        }

        double minimumX = workArea.Left + safeMargin;
        double maximumX = workArea.Right - safeMargin - petWidth;
        double minimumY = workArea.Top + safeMargin;
        double maximumY = workArea.Bottom - safeMargin - petHeight;
        return new PointerPoint(
            PickWithin(minimumX, maximumX),
            PickWithin(minimumY, maximumY));
    }

    private double PickWithin(double minimum, double maximum)
    {
        if (maximum <= minimum)
        {
            return minimum;
        }

        double unit = _nextUnitValue();
        if (!double.IsFinite(unit) || unit is < 0 or > 1)
        {
            throw new InvalidOperationException("Random position source must return a value from 0 to 1.");
        }

        return minimum + ((maximum - minimum) * unit);
    }
}
