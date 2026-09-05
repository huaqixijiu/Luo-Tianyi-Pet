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
}
