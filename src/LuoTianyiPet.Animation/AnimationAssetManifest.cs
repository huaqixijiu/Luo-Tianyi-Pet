namespace LuoTianyiPet.Animation;

public sealed record AnimationAssetManifest
{
    public required string Id { get; init; }

    public required string SourcePath { get; init; }

    public required string SourceSha256 { get; init; }

    public required string AtlasPath { get; init; }

    public required string AtlasSha256 { get; init; }

    public required int FrameWidth { get; init; }

    public required int FrameHeight { get; init; }

    public required int Columns { get; init; }

    public required int Rows { get; init; }

    public required IReadOnlyList<int> FrameDurationsMilliseconds { get; init; }

    public required int LoopCount { get; init; }

    public required int DisplayWidth { get; init; }

    public required int DisplayHeight { get; init; }

    public required double AnchorX { get; init; }

    public required double AnchorY { get; init; }

    public required IReadOnlyList<int> AlphaBounds { get; init; }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(Id))
        {
            errors.Add("Animation id is required.");
        }

        if (string.IsNullOrWhiteSpace(AtlasPath))
        {
            errors.Add($"Animation '{Id}' requires an atlas path.");
        }

        if (FrameWidth <= 0 || FrameHeight <= 0)
        {
            errors.Add($"Animation '{Id}' frame dimensions must be positive.");
        }

        if (Columns <= 0 || Rows <= 0 || FrameDurationsMilliseconds.Count > Columns * Rows)
        {
            errors.Add($"Animation '{Id}' atlas grid cannot contain all declared frames.");
        }

        if (FrameDurationsMilliseconds.Count == 0 || FrameDurationsMilliseconds.Any(duration => duration <= 0))
        {
            errors.Add($"Animation '{Id}' frame durations must be positive.");
        }

        if (LoopCount < 0)
        {
            errors.Add($"Animation '{Id}' loop count cannot be negative.");
        }

        if (DisplayWidth <= 0 || DisplayHeight <= 0)
        {
            errors.Add($"Animation '{Id}' display dimensions must be positive.");
        }

        if (!double.IsFinite(AnchorX) || !double.IsFinite(AnchorY) ||
            AnchorX is < 0 or > 1 || AnchorY is < 0 or > 1)
        {
            errors.Add($"Animation '{Id}' anchor must be normalized to 0..1.");
        }

        if (AlphaBounds.Count != 4)
        {
            errors.Add($"Animation '{Id}' alpha bounds must contain four values.");
        }

        return errors;
    }
}
