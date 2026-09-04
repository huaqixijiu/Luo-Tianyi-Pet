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
        new(BodyRegionId.LeftEye, new(0.39, 0.20, 0.10, 0.10)),
        new(BodyRegionId.RightEye, new(0.51, 0.20, 0.10, 0.10)),
        new(BodyRegionId.Mouth, new(0.455, 0.28, 0.09, 0.055)),
        new(BodyRegionId.Face, new(0.36, 0.15, 0.28, 0.20)),
        new(BodyRegionId.LeftHand, new(0.20, 0.49, 0.12, 0.14)),
        new(BodyRegionId.RightHand, new(0.68, 0.49, 0.12, 0.14)),
        new(BodyRegionId.Chest, new(0.455, 0.34, 0.09, 0.12)),
        new(BodyRegionId.LowerBodySensitiveArea, new(0.465, 0.54, 0.07, 0.09)),
        new(BodyRegionId.LeftFoot, new(0.39, 0.82, 0.11, 0.16)),
        new(BodyRegionId.RightFoot, new(0.50, 0.82, 0.11, 0.16)),
        new(BodyRegionId.HeadAndHair, new(0.29, 0.02, 0.42, 0.39)),
        new(BodyRegionId.OtherBody, new(0.08, 0.02, 0.84, 0.96)),
    ]);

    public static BodyHitMap CrystalDress { get; } = new(
    [
        new(BodyRegionId.LeftEye, new(0.37, 0.20, 0.12, 0.11)),
        new(BodyRegionId.RightEye, new(0.51, 0.20, 0.12, 0.11)),
        new(BodyRegionId.Mouth, new(0.455, 0.30, 0.09, 0.05)),
        new(BodyRegionId.Face, new(0.33, 0.12, 0.34, 0.25)),
        new(BodyRegionId.LeftHand, new(0.23, 0.50, 0.13, 0.14)),
        new(BodyRegionId.RightHand, new(0.64, 0.50, 0.13, 0.14)),
        new(BodyRegionId.Chest, new(0.455, 0.38, 0.09, 0.11)),
        new(BodyRegionId.LowerBodySensitiveArea, new(0.465, 0.61, 0.07, 0.08)),
        new(BodyRegionId.LeftFoot, new(0.39, 0.84, 0.11, 0.14)),
        new(BodyRegionId.RightFoot, new(0.50, 0.84, 0.11, 0.14)),
        new(BodyRegionId.HeadAndHair, new(0.25, 0.01, 0.50, 0.39)),
        new(BodyRegionId.OtherBody, new(0.08, 0.01, 0.84, 0.97)),
    ]);

    public static BodyHitMap ClassicCatEars { get; } = new(
    [
        new(BodyRegionId.LeftEye, new(0.34, 0.26, 0.14, 0.13)),
        new(BodyRegionId.RightEye, new(0.52, 0.26, 0.14, 0.13)),
        new(BodyRegionId.Mouth, new(0.45, 0.39, 0.10, 0.055)),
        new(BodyRegionId.Face, new(0.29, 0.16, 0.42, 0.31)),
        new(BodyRegionId.LeftHand, new(0.25, 0.57, 0.13, 0.13)),
        new(BodyRegionId.RightHand, new(0.62, 0.57, 0.13, 0.13)),
        new(BodyRegionId.Chest, new(0.455, 0.48, 0.09, 0.10)),
        new(BodyRegionId.LowerBodySensitiveArea, new(0.465, 0.68, 0.07, 0.08)),
        new(BodyRegionId.LeftFoot, new(0.39, 0.84, 0.11, 0.13)),
        new(BodyRegionId.RightFoot, new(0.50, 0.84, 0.11, 0.13)),
        new(BodyRegionId.HeadAndHair, new(0.20, 0.02, 0.60, 0.47)),
        new(BodyRegionId.OtherBody, new(0.10, 0.02, 0.80, 0.95)),
    ]);

    public static BodyHitMap ForFullBodyAnimation(string? animationId) => animationId switch
    {
        AppearanceOptionIds.CrystalDressAnimation => CrystalDress,
        AppearanceOptionIds.ClassicCatEarsAnimation => ClassicCatEars,
        _ => FullBodyDefault,
    };

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
