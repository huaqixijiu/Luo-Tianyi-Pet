namespace LuoTianyiPet.Core;

public sealed record AudioSessionSnapshot(
    bool ProbeSucceeded,
    bool TargetSessionFound,
    float PeakLevel)
{
    public static AudioSessionSnapshot Unavailable { get; } = new(false, false, 0);

    public static AudioSessionSnapshot Missing { get; } = new(true, false, 0);

    public static AudioSessionSnapshot Found(float peakLevel)
    {
        if (!float.IsFinite(peakLevel) || peakLevel is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(peakLevel));
        }

        return new AudioSessionSnapshot(true, true, peakLevel);
    }
}

public interface IAudioSessionProbe
{
    AudioSessionSnapshot ReadForProcess(string processName);
}
