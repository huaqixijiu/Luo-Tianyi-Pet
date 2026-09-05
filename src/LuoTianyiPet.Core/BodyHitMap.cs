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

public sealed class NormalizedPolygon
{
    public NormalizedPolygon(IReadOnlyList<PointerPoint> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        if (vertices.Count < 3)
        {
            throw new ArgumentException("A polygon requires at least three vertices.", nameof(vertices));
        }

        foreach (PointerPoint vertex in vertices)
        {
            if (!double.IsFinite(vertex.X) || !double.IsFinite(vertex.Y) ||
                vertex.X < 0 || vertex.X > 1 || vertex.Y < 0 || vertex.Y > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(vertices));
            }
        }

        Vertices = vertices.ToArray();
        double minX = Vertices.Min(vertex => vertex.X);
        double maxX = Vertices.Max(vertex => vertex.X);
        double minY = Vertices.Min(vertex => vertex.Y);
        double maxY = Vertices.Max(vertex => vertex.Y);
        Bounds = new NormalizedRectangle(minX, minY, maxX - minX, maxY - minY);
        Bounds.Validate();
    }

    public IReadOnlyList<PointerPoint> Vertices { get; }

    public NormalizedRectangle Bounds { get; }

    public bool Contains(PointerPoint point)
    {
        if (!Bounds.Contains(point))
        {
            return false;
        }

        bool inside = false;
        for (int current = 0, previous = Vertices.Count - 1;
             current < Vertices.Count;
             previous = current++)
        {
            PointerPoint a = Vertices[previous];
            PointerPoint b = Vertices[current];
            if (IsOnSegment(point, a, b))
            {
                return true;
            }

            bool crossesScanline = (a.Y > point.Y) != (b.Y > point.Y);
            if (crossesScanline &&
                point.X < ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static NormalizedPolygon FromRectangle(NormalizedRectangle bounds)
    {
        bounds.Validate();
        return new NormalizedPolygon(
        [
            new(bounds.X, bounds.Y),
            new(bounds.X + bounds.Width, bounds.Y),
            new(bounds.X + bounds.Width, bounds.Y + bounds.Height),
            new(bounds.X, bounds.Y + bounds.Height),
        ]);
    }

    private static bool IsOnSegment(PointerPoint point, PointerPoint a, PointerPoint b)
    {
        const double epsilon = 1e-9;
        double cross = ((point.Y - a.Y) * (b.X - a.X)) -
            ((point.X - a.X) * (b.Y - a.Y));
        if (Math.Abs(cross) > epsilon)
        {
            return false;
        }

        return point.X >= Math.Min(a.X, b.X) - epsilon &&
            point.X <= Math.Max(a.X, b.X) + epsilon &&
            point.Y >= Math.Min(a.Y, b.Y) - epsilon &&
            point.Y <= Math.Max(a.Y, b.Y) + epsilon;
    }
}

public sealed class BodyHitRegion
{
    public BodyHitRegion(BodyRegionId id, NormalizedRectangle bounds)
        : this(id, [NormalizedPolygon.FromRectangle(bounds)])
    {
    }

    private BodyHitRegion(BodyRegionId id, IReadOnlyList<NormalizedPolygon> polygons)
    {
        ArgumentNullException.ThrowIfNull(polygons);
        if (polygons.Count == 0)
        {
            throw new ArgumentException("A body region requires at least one polygon.", nameof(polygons));
        }

        Id = id;
        Polygons = polygons.ToArray();
        double minX = Polygons.Min(polygon => polygon.Bounds.X);
        double maxX = Polygons.Max(polygon => polygon.Bounds.X + polygon.Bounds.Width);
        double minY = Polygons.Min(polygon => polygon.Bounds.Y);
        double maxY = Polygons.Max(polygon => polygon.Bounds.Y + polygon.Bounds.Height);
        Bounds = new NormalizedRectangle(minX, minY, maxX - minX, maxY - minY);
        Bounds.Validate();
    }

    public BodyRegionId Id { get; }

    public IReadOnlyList<NormalizedPolygon> Polygons { get; }

    public NormalizedRectangle Bounds { get; }

    public bool Contains(PointerPoint point) => Polygons.Any(polygon => polygon.Contains(point));

    public static BodyHitRegion FromPolygons(
        BodyRegionId id,
        IReadOnlyList<NormalizedPolygon> polygons) => new(id, polygons);
}

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
        new(BodyRegionId.LeftEye, new(0.33, 0.36, 0.16, 0.14)),
        new(BodyRegionId.RightEye, new(0.56, 0.36, 0.15, 0.14)),
        new(BodyRegionId.Mouth, new(0.46, 0.49, 0.09, 0.06)),
        new(BodyRegionId.Face, new(0.29, 0.29, 0.42, 0.27)),
        new(BodyRegionId.LeftHand, new(0.27, 0.72, 0.12, 0.13)),
        new(BodyRegionId.RightHand, new(0.62, 0.72, 0.12, 0.13)),
        new(BodyRegionId.Chest, new(0.46, 0.57, 0.08, 0.08)),
        new(BodyRegionId.LowerBodySensitiveArea, new(0.465, 0.72, 0.07, 0.08)),
        new(BodyRegionId.LeftFoot, new(0.36, 0.89, 0.13, 0.10)),
        new(BodyRegionId.RightFoot, new(0.54, 0.89, 0.14, 0.10)),
        new(BodyRegionId.HeadAndHair, new(0.14, 0.02, 0.72, 0.54)),
        new(BodyRegionId.OtherBody, new(0.10, 0.02, 0.80, 0.97)),
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

        HashSet<BodyRegionId> ids = [];
        foreach (BodyHitRegion region in regions)
        {
            if (!ids.Add(region.Id))
            {
                throw new ArgumentException($"Duplicate body region: {region.Id}.", nameof(regions));
            }
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
            if (region.Contains(normalizedPoint))
            {
                return region.Id;
            }
        }

        return null;
    }
}
