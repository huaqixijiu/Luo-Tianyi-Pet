namespace LuoTianyiPet.Core;

public sealed class DesktopItemDisappearedEventArgs(
    PointerPoint screenPositionPixels,
    bool usedCachedIconPosition) : EventArgs
{
    public PointerPoint ScreenPositionPixels { get; } = screenPositionPixels;

    public bool UsedCachedIconPosition { get; } = usedCachedIconPosition;
}

public interface IDesktopItemDisappearanceSource : IDisposable
{
    event EventHandler<DesktopItemDisappearedEventArgs>? ItemDisappeared;

    void Start();

    void Stop();
}

public readonly record struct BunChaseStep(PointerPoint Position, bool Arrived);

public static class DesktopFileTreatSafety
{
    public static bool AllowsForeground(
        ForegroundApplicationSnapshot foreground,
        bool protectedApplicationForeground)
    {
        bool explorerForeground = string.Equals(
            Path.GetFileNameWithoutExtension(foreground.ProcessName),
            "explorer",
            StringComparison.OrdinalIgnoreCase);
        return foreground.Succeeded &&
            (!foreground.IsFullscreen || explorerForeground) &&
            !protectedApplicationForeground;
    }
}

public static class BunChasePlanner
{
    private const double ReferenceDesktopWidth = 1920;
    private const double ReferenceDesktopHeight = 1080;

    public static double ResolveDesktopSpeedScale(
        double desktopWidthDips,
        double desktopHeightDips,
        double maximumScale = 2)
    {
        if (!double.IsFinite(desktopWidthDips) || desktopWidthDips <= 0 ||
            !double.IsFinite(desktopHeightDips) || desktopHeightDips <= 0 ||
            !double.IsFinite(maximumScale) || maximumScale < 1)
        {
            return 1;
        }

        double referenceDiagonal = Math.Sqrt(
            ReferenceDesktopWidth * ReferenceDesktopWidth +
            ReferenceDesktopHeight * ReferenceDesktopHeight);
        double desktopDiagonal = Math.Sqrt(
            desktopWidthDips * desktopWidthDips + desktopHeightDips * desktopHeightDips);
        return Math.Clamp(desktopDiagonal / referenceDiagonal, 1, maximumScale);
    }

    public static double ResolveDesktopSpeedScaleFromPixels(
        double desktopWidthPixels,
        double desktopHeightPixels,
        double dpiScaleX,
        double dpiScaleY,
        double maximumScale = 2)
    {
        double safeDpiScaleX = double.IsFinite(dpiScaleX) && dpiScaleX > 0 ? dpiScaleX : 1;
        double safeDpiScaleY = double.IsFinite(dpiScaleY) && dpiScaleY > 0 ? dpiScaleY : 1;
        return ResolveDesktopSpeedScale(
            desktopWidthPixels / safeDpiScaleX,
            desktopHeightPixels / safeDpiScaleY,
            maximumScale);
    }

    public static bool ShouldInterruptReturnForQueuedTreat(
        bool chaseActive,
        bool returning,
        bool eating,
        int queuedBunCount) =>
        chaseActive && returning && !eating && queuedBunCount > 0;

    public static double ResolveAcceleratedSpeed(
        double startingSpeedPerSecond,
        double originalCruiseSpeedPerSecond,
        TimeSpan elapsedSinceRunStarted,
        TimeSpan accelerationDuration,
        double maximumMultiplier = 3)
    {
        double start = Math.Max(0, startingSpeedPerSecond);
        double maximum = Math.Max(0, originalCruiseSpeedPerSecond) *
            Math.Max(1, maximumMultiplier);
        if (accelerationDuration <= TimeSpan.Zero)
        {
            return maximum;
        }

        double progress = Math.Clamp(
            Math.Max(0, elapsedSinceRunStarted.TotalSeconds) /
                accelerationDuration.TotalSeconds,
            0,
            1);
        return start + (maximum - start) * progress;
    }

    public static PointerPoint ResolveMouthTarget(
        PointerPoint imageTopLeft,
        double imageWidth,
        double imageHeight,
        bool mirrored,
        double unmirroredXFraction = 0.60,
        double yFraction = 0.535) =>
        new(
            imageTopLeft.X + Math.Max(0, imageWidth) * (
                mirrored
                    ? 1.0 - Math.Clamp(unmirroredXFraction, 0, 1)
                    : Math.Clamp(unmirroredXFraction, 0, 1)),
            imageTopLeft.Y + Math.Max(0, imageHeight) * Math.Clamp(yFraction, 0, 1));

    public static double AdvanceSpeed(
        double currentSpeedPerSecond,
        double targetSpeedPerSecond,
        double accelerationPerSecondSquared,
        TimeSpan elapsed)
    {
        double current = Math.Max(0, currentSpeedPerSecond);
        double target = Math.Max(0, targetSpeedPerSecond);
        double delta = Math.Max(0, accelerationPerSecondSquared) * Math.Max(0, elapsed.TotalSeconds);
        return current <= target
            ? Math.Min(target, current + delta)
            : Math.Max(target, current - delta);
    }

    public static BunChaseStep Advance(
        PointerPoint current,
        PointerPoint target,
        double speedPerSecond,
        TimeSpan elapsed,
        double arrivalRadius)
    {
        double dx = target.X - current.X;
        double dy = target.Y - current.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= arrivalRadius)
        {
            return new BunChaseStep(target, true);
        }

        double maximumStep = Math.Max(0, speedPerSecond) * Math.Max(0, elapsed.TotalSeconds);
        if (maximumStep >= distance - arrivalRadius)
        {
            double travel = Math.Max(0, distance - arrivalRadius);
            return new BunChaseStep(
                new PointerPoint(current.X + dx / distance * travel, current.Y + dy / distance * travel),
                true);
        }

        return new BunChaseStep(
            new PointerPoint(
                current.X + dx / distance * maximumStep,
                current.Y + dy / distance * maximumStep),
            false);
    }

    public static TimeSpan EstimateTravelDuration(
        double distance,
        double startingSpeedPerSecond,
        double maximumSpeedPerSecond,
        TimeSpan accelerationDuration)
    {
        double travelDistance = Math.Max(0, distance);
        double start = Math.Max(0, startingSpeedPerSecond);
        double maximum = Math.Max(start, maximumSpeedPerSecond);
        double accelerationSeconds = Math.Max(0, accelerationDuration.TotalSeconds);
        if (travelDistance <= 0)
        {
            return TimeSpan.Zero;
        }

        if (maximum <= 0)
        {
            return TimeSpan.MaxValue;
        }

        if (accelerationSeconds <= 0 || maximum == start)
        {
            return TimeSpan.FromSeconds(travelDistance / maximum);
        }

        double acceleration = (maximum - start) / accelerationSeconds;
        double accelerationDistance = (start + maximum) * 0.5 * accelerationSeconds;
        if (travelDistance <= accelerationDistance)
        {
            double seconds = (-start + Math.Sqrt(
                start * start + 2 * acceleration * travelDistance)) / acceleration;
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromSeconds(
            accelerationSeconds + (travelDistance - accelerationDistance) / maximum);
    }
}
