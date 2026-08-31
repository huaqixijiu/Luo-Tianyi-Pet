namespace LuoTianyiPet.Animation;

public readonly record struct EdgeDockFrameRoute(
    int StartFrameIndex,
    int EndFrameIndex);

public static class EdgeDockFrameRouteResolver
{
    public static EdgeDockFrameRoute Resolve(
        bool reveal,
        int? currentFrameIndex,
        int hiddenFrameIndex,
        int hideStartFrameIndex,
        int revealEndFrameIndex)
    {
        if (hiddenFrameIndex < 0 ||
            hideStartFrameIndex < hiddenFrameIndex ||
            revealEndFrameIndex < hideStartFrameIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hiddenFrameIndex),
                "Expected 0 <= hidden frame <= hide start frame <= reveal end frame.");
        }

        if (reveal)
        {
            int start = currentFrameIndex is int current &&
                current >= hiddenFrameIndex && current <= revealEndFrameIndex
                    ? current
                    : hiddenFrameIndex;
            return new EdgeDockFrameRoute(start, revealEndFrameIndex);
        }

        int hideStart = currentFrameIndex is int currentHide &&
            currentHide >= hiddenFrameIndex && currentHide <= hideStartFrameIndex
                ? currentHide
                : hideStartFrameIndex;
        return new EdgeDockFrameRoute(hideStart, hiddenFrameIndex);
    }
}
