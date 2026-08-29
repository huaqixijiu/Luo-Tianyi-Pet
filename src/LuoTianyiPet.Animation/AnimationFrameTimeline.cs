namespace LuoTianyiPet.Animation;

public readonly record struct PlaybackFrame(int Index, bool IsCompleted);

public sealed class AnimationFrameTimeline
{
    private readonly long[] _frameEndMilliseconds;
    private readonly int _loopCount;

    public AnimationFrameTimeline(IReadOnlyList<int> frameDurationsMilliseconds, int loopCount)
    {
        ArgumentNullException.ThrowIfNull(frameDurationsMilliseconds);
        if (frameDurationsMilliseconds.Count == 0 || frameDurationsMilliseconds.Any(value => value <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameDurationsMilliseconds),
                "Frame durations must contain positive values.");
        }

        if (loopCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(loopCount));
        }

        _loopCount = loopCount;
        _frameEndMilliseconds = new long[frameDurationsMilliseconds.Count];
        long total = 0;
        for (int index = 0; index < frameDurationsMilliseconds.Count; index++)
        {
            total = checked(total + frameDurationsMilliseconds[index]);
            _frameEndMilliseconds[index] = total;
        }

        CycleDurationMilliseconds = total;
    }

    public long CycleDurationMilliseconds { get; }

    public PlaybackFrame GetFrame(TimeSpan elapsed)
    {
        long elapsedMilliseconds = Math.Max(0, (long)elapsed.TotalMilliseconds);
        if (_loopCount > 0 && elapsedMilliseconds >= CycleDurationMilliseconds * _loopCount)
        {
            return new PlaybackFrame(_frameEndMilliseconds.Length - 1, true);
        }

        long position = elapsedMilliseconds % CycleDurationMilliseconds;
        int index = Array.BinarySearch(_frameEndMilliseconds, position + 1);
        if (index < 0)
        {
            index = ~index;
        }

        return new PlaybackFrame(index, false);
    }
}
