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
