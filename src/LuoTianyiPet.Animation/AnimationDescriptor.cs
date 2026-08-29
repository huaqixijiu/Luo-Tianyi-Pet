namespace LuoTianyiPet.Animation;

public sealed record AnimationDescriptor(
    string Id,
    string AssetPath,
    int LoopCount,
    double PlaybackRate = 1.0)
{
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(Id))
        {
            errors.Add("Animation id is required.");
        }

        if (string.IsNullOrWhiteSpace(AssetPath))
        {
            errors.Add("Animation asset path is required.");
        }

        if (LoopCount < 0)
        {
            errors.Add("Loop count cannot be negative; use zero for an indefinite loop.");
        }

        if (!double.IsFinite(PlaybackRate) || PlaybackRate <= 0)
        {
            errors.Add("Playback rate must be a finite positive value.");
        }

        return errors;
    }
}
