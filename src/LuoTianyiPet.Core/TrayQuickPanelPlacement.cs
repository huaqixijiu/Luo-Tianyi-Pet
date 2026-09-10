namespace LuoTianyiPet.Core;

public readonly record struct TrayQuickPanelPosition(double Left, double Top);

public static class TrayQuickPanelPlacement
{
    public static TrayQuickPanelPosition Resolve(
        PointerPoint pointer,
        DesktopRectangle panel,
        DesktopRectangle workArea,
        double gap = 10,
        double safePadding = 8)
    {
        if (panel.Width <= 0 || panel.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(panel));
        }

        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea));
        }

        double minimumLeft = workArea.Left + safePadding;
        double maximumLeft = Math.Max(minimumLeft, workArea.Right - panel.Width - safePadding);
        double preferredLeft = pointer.X - panel.Width + 28;
        double left = Numeric.Clamp(preferredLeft, minimumLeft, maximumLeft);

        double minimumTop = workArea.Top + safePadding;
        double maximumTop = Math.Max(minimumTop, workArea.Bottom - panel.Height - safePadding);
        double above = pointer.Y - panel.Height - gap;
        double below = pointer.Y + gap;
        double top = above >= minimumTop
            ? above
            : below <= maximumTop
                ? below
                : Numeric.Clamp(pointer.Y - (panel.Height / 2), minimumTop, maximumTop);

        return new TrayQuickPanelPosition(left, top);
    }
}
