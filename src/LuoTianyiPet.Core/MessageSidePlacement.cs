namespace LuoTianyiPet.Core;

public readonly record struct MessageSidePosition(double Left, double Top, bool MirrorCharacter);

public static class MessageSidePlacement
{
    // All measurements use one coordinate system, normally physical screen pixels.
    public static MessageSidePosition Resolve(DesktopRectangle character, double width, double height,
        DesktopRectangle work, double gap = 4, double padding = 8)
    {
        if (width <= 0 || height <= 0 || character.Width <= 0 || character.Height <= 0 ||
            work.Width <= 0 || work.Height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        double right = character.Right + gap;
        double left = character.Left - width - gap;
        bool mirror = right + width > work.Right - padding &&
            (left >= work.Left + padding || character.Left - work.Left > work.Right - character.Right);
        double x = Numeric.Clamp(mirror ? left : right, work.Left + padding,
            Math.Max(work.Left + padding, work.Right - width - padding));
        double y = Numeric.Clamp(character.Top + character.Height * 0.63 - height / 2,
            work.Top + padding, Math.Max(work.Top + padding, work.Bottom - height - padding));
        return new(x, y, mirror);
    }
}
