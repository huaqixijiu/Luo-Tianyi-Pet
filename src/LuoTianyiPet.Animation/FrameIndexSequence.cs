namespace LuoTianyiPet.Animation;

public static class FrameIndexSequence
{
    public static IReadOnlyList<int> Create(int start, int end, int frameCount)
    {
        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        }

        if ((uint)start >= (uint)frameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if ((uint)end >= (uint)frameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        int step = start <= end ? 1 : -1;
        int length = Math.Abs(end - start) + 1;
        int[] indices = new int[length];
        for (int offset = 0; offset < length; offset++)
        {
            indices[offset] = start + offset * step;
        }

        return indices;
    }
}
