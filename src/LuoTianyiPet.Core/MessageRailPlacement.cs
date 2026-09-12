namespace LuoTianyiPet.Core;

public enum MessageRailSide { Left, Right }
public sealed record MessageRailLayout(DesktopRectangle Icons, DesktopRectangle Panel, MessageRailSide Side);

public static class MessageRailPlacement
{
    public const double IconSize = 24, IconGap = 6, GroupWidth = 38, PanelWidth = 192, RowHeight = 46, PanelGap = 6;
    public static MessageRailSide SideFor(DesktopRectangle pet, DesktopRectangle work,
        MessageRailSide? previous, bool freeze)
    {
        double center = pet.Left + pet.Width / 2, middle = work.Left + work.Width / 2;
        if (previous is MessageRailSide side && (freeze || Math.Abs(center - middle) < Math.Max(48, work.Width * .06))) return side;
        return center >= middle ? MessageRailSide.Left : MessageRailSide.Right;
    }
    public static MessageRailLayout Place(DesktopRectangle contour, double earY, DesktopRectangle work,
        MessageRailSide side, int iconCount, int rows, int activeIcon, double scale = 1)
    {
        double iw = GroupWidth * scale, ih = Math.Max(1, iconCount) * (IconSize + IconGap) * scale + 4 * scale;
        double ix = side == MessageRailSide.Left ? contour.Left - iw - 3 * scale : contour.Right + 3 * scale;
        ix = Numeric.Clamp(ix, work.Left + 2 * scale, Math.Max(work.Left + 2 * scale, work.Right - iw - 2 * scale));
        double iy = Numeric.Clamp(earY - 10 * scale, work.Top + 3 * scale, Math.Max(work.Top + 3 * scale, work.Bottom - ih - 3 * scale));
        double pw = Math.Min(PanelWidth * scale, Math.Max(1, work.Width - iw - 16 * scale));
        double ph = Math.Min(Math.Min(3, Math.Max(1, rows)) * RowHeight * scale + 4 * scale, Math.Max(1, work.Height - 8 * scale));
        double px = side == MessageRailSide.Left ? ix - PanelGap * scale - pw : ix + iw + PanelGap * scale;
        px = Numeric.Clamp(px, work.Left + 3 * scale, Math.Max(work.Left + 3 * scale, work.Right - pw - 3 * scale));
        double py = Numeric.Clamp(iy + activeIcon * (IconSize + IconGap) * scale,
            work.Top + 4 * scale, Math.Max(work.Top + 4 * scale, work.Bottom - ph - 4 * scale));
        return new(new(ix, iy, iw, ih), new(px, py, pw, ph), side);
    }
}
