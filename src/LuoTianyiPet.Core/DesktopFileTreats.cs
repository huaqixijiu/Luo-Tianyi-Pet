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
        if (!Numeric.IsFinite(desktopWidthDips) || desktopWidthDips <= 0 ||
            !Numeric.IsFinite(desktopHeightDips) || desktopHeightDips <= 0 ||
            !Numeric.IsFinite(maximumScale) || maximumScale < 1)
        {
            return 1;
        }

        double referenceDiagonal = Math.Sqrt(
            ReferenceDesktopWidth * ReferenceDesktopWidth +
            ReferenceDesktopHeight * ReferenceDesktopHeight);
        double desktopDiagonal = Math.Sqrt(
            desktopWidthDips * desktopWidthDips + desktopHeightDips * desktopHeightDips);
        return Numeric.Clamp(desktopDiagonal / referenceDiagonal, 1, maximumScale);
    }

    public static double ResolveDesktopSpeedScaleFromPixels(
        double desktopWidthPixels,
        double desktopHeightPixels,
        double dpiScaleX,
        double dpiScaleY,
        double maximumScale = 2)
    {
        double safeDpiScaleX = Numeric.IsFinite(dpiScaleX) && dpiScaleX > 0 ? dpiScaleX : 1;
        double safeDpiScaleY = Numeric.IsFinite(dpiScaleY) && dpiScaleY > 0 ? dpiScaleY : 1;
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

    public static double ResolveSpeedTowardMaximum(
        double startingSpeedPerSecond,
        double maximumSpeedPerSecond,
        TimeSpan elapsedSinceRunStarted,
        TimeSpan accelerationDuration)
    {
        double start = Math.Max(0, startingSpeedPerSecond);
        double maximum = Math.Max(start, maximumSpeedPerSecond);
        if (accelerationDuration <= TimeSpan.Zero)
        {
            return maximum;
        }

        double progress = Numeric.Clamp(
            Math.Max(0, elapsedSinceRunStarted.TotalSeconds) /
                accelerationDuration.TotalSeconds,
            0,
            1);
        double eased = progress * progress * (3 - 2 * progress);
        return start + (maximum - start) * eased;
    }

    public static bool ShouldShowBunRequest(
        int queuedBunCount,
        bool reachedMaximumSpeed,
        bool targetIsBeingDragged,
        TimeSpan continuousDragDuration,
        TimeSpan requiredDragDuration,
        bool alreadyShown) =>
        queuedBunCount == 1 &&
        reachedMaximumSpeed &&
        targetIsBeingDragged &&
        continuousDragDuration >= requiredDragDuration &&
        !alreadyShown;

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
                    ? 1.0 - Numeric.Clamp(unmirroredXFraction, 0, 1)
                    : Numeric.Clamp(unmirroredXFraction, 0, 1)),
            imageTopLeft.Y + Math.Max(0, imageHeight) * Numeric.Clamp(yFraction, 0, 1));

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

public static class BunFeedHitTester
{
    public static bool HasOpaqueOverlap(
        ReadOnlySpan<byte> alpha,
        int pixelWidth,
        int pixelHeight,
        PointerPoint imageTopLeft,
        double imageWidth,
        double imageHeight,
        PointerPoint treatTopLeft,
        double treatWidth,
        double treatHeight,
        byte alphaThreshold = 24)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 ||
            alpha.Length < pixelWidth * pixelHeight ||
            imageWidth <= 0 || imageHeight <= 0 ||
            treatWidth <= 0 || treatHeight <= 0)
        {
            return false;
        }

        double intersectionLeft = Math.Max(imageTopLeft.X, treatTopLeft.X);
        double intersectionTop = Math.Max(imageTopLeft.Y, treatTopLeft.Y);
        double intersectionRight = Math.Min(
            imageTopLeft.X + imageWidth,
            treatTopLeft.X + treatWidth);
        double intersectionBottom = Math.Min(
            imageTopLeft.Y + imageHeight,
            treatTopLeft.Y + treatHeight);
        if (intersectionRight <= intersectionLeft || intersectionBottom <= intersectionTop)
        {
            return false;
        }

        int startX = Numeric.Clamp(
            (int)Math.Floor((intersectionLeft - imageTopLeft.X) / imageWidth * pixelWidth),
            0,
            pixelWidth - 1);
        int endX = Numeric.Clamp(
            (int)Math.Ceiling((intersectionRight - imageTopLeft.X) / imageWidth * pixelWidth),
            startX + 1,
            pixelWidth);
        int startY = Numeric.Clamp(
            (int)Math.Floor((intersectionTop - imageTopLeft.Y) / imageHeight * pixelHeight),
            0,
            pixelHeight - 1);
        int endY = Numeric.Clamp(
            (int)Math.Ceiling((intersectionBottom - imageTopLeft.Y) / imageHeight * pixelHeight),
            startY + 1,
            pixelHeight);
        double centreX = treatTopLeft.X + treatWidth / 2;
        double centreY = treatTopLeft.Y + treatHeight / 2;
        double radiusX = treatWidth / 2;
        double radiusY = treatHeight / 2;

        for (int y = startY; y < endY; y++)
        {
            double screenY = imageTopLeft.Y + (y + 0.5) / pixelHeight * imageHeight;
            double normalizedY = (screenY - centreY) / radiusY;
            for (int x = startX; x < endX; x++)
            {
                double screenX = imageTopLeft.X + (x + 0.5) / pixelWidth * imageWidth;
                double normalizedX = (screenX - centreX) / radiusX;
                if (normalizedX * normalizedX + normalizedY * normalizedY <= 1 &&
                    alpha[y * pixelWidth + x] >= alphaThreshold)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
