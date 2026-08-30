namespace LuoTianyiPet.Core;

public enum BodyRegionId
{
    LeftEye,
    RightEye,
    Mouth,
    Face,
    LeftHand,
    RightHand,
    Chest,
    LowerBodySensitiveArea,
    LeftFoot,
    RightFoot,
    HeadAndHair,
    OtherBody,
}

public readonly record struct NormalizedRectangle(double X, double Y, double Width, double Height)
{
    public bool Contains(PointerPoint point) =>
        point.X >= X && point.X <= X + Width &&
        point.Y >= Y && point.Y <= Y + Height;

    public void Validate()
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y) ||
            !double.IsFinite(Width) || !double.IsFinite(Height) ||
            X < 0 || Y < 0 || Width <= 0 || Height <= 0 ||
            X + Width > 1 || Y + Height > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(NormalizedRectangle));
        }
    }
}

public sealed record BodyHitRegion(BodyRegionId Id, NormalizedRectangle Bounds);

public sealed class BodyHitMap
{
    public static BodyHitMap FullBodyDefault { get; } = new(
    [
        new(BodyRegionId.LeftEye, new(0.35, 0.39, 0.10, 0.10)),
        new(BodyRegionId.RightEye, new(0.56, 0.39, 0.10, 0.10)),
        new(BodyRegionId.Mouth, new(0.43, 0.49, 0.15, 0.07)),
        new(BodyRegionId.Face, new(0.29, 0.34, 0.42, 0.24)),
        new(BodyRegionId.LeftHand, new(0.20, 0.62, 0.18, 0.15)),
        new(BodyRegionId.RightHand, new(0.68, 0.62, 0.18, 0.15)),
        new(BodyRegionId.Chest, new(0.44, 0.59, 0.12, 0.10)),
        new(BodyRegionId.LowerBodySensitiveArea, new(0.46, 0.76, 0.09, 0.09)),
        new(BodyRegionId.LeftFoot, new(0.37, 0.89, 0.14, 0.10)),
        new(BodyRegionId.RightFoot, new(0.51, 0.89, 0.18, 0.10)),
        new(BodyRegionId.HeadAndHair, new(0.15, 0.03, 0.72, 0.60)),
        new(BodyRegionId.OtherBody, new(0.14, 0.03, 0.73, 0.96)),
    ]);

    public BodyHitMap(IReadOnlyList<BodyHitRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        if (regions.Count == 0)
        {
            throw new ArgumentException("At least one body region is required.", nameof(regions));
        }

        foreach (BodyHitRegion region in regions)
        {
            region.Bounds.Validate();
        }

        Regions = regions.ToArray();
    }

    public IReadOnlyList<BodyHitRegion> Regions { get; }

    public BodyRegionId? HitTest(PointerPoint normalizedPoint)
    {
        if (!double.IsFinite(normalizedPoint.X) || !double.IsFinite(normalizedPoint.Y) ||
            normalizedPoint.X < 0 || normalizedPoint.X > 1 ||
            normalizedPoint.Y < 0 || normalizedPoint.Y > 1)
        {
            return null;
        }

        foreach (BodyHitRegion region in Regions)
        {
            if (region.Bounds.Contains(normalizedPoint))
            {
                return region.Id;
            }
        }

        return null;
    }
}
