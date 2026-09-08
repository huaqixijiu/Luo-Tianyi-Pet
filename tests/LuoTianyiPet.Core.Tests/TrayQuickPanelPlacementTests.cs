using Xunit;

namespace LuoTianyiPet.Core.Tests;

public sealed class TrayQuickPanelPlacementTests
{
    private static readonly DesktopRectangle WorkArea = new(0, 0, 1920, 1040);
    private static readonly DesktopRectangle Panel = new(0, 0, 324, 456);

    [Fact]
    public void ResolvePlacesPanelAboveBottomRightTrayPoint()
    {
        TrayQuickPanelPosition position = TrayQuickPanelPlacement.Resolve(
            new PointerPoint(1900, 1020),
            Panel,
            WorkArea);

        Assert.Equal(1588, position.Left);
        Assert.Equal(554, position.Top);
    }

    [Fact]
    public void ResolvePlacesPanelBelowPointerWhenThereIsNoRoomAbove()
    {
        TrayQuickPanelPosition position = TrayQuickPanelPlacement.Resolve(
            new PointerPoint(22, 18),
            Panel,
            WorkArea);

        Assert.Equal(8, position.Left);
        Assert.Equal(28, position.Top);
    }

    [Fact]
    public void ResolveKeepsOversizedPanelAtSafeOrigin()
    {
        TrayQuickPanelPosition position = TrayQuickPanelPlacement.Resolve(
            new PointerPoint(400, 300),
            new DesktopRectangle(0, 0, 1200, 900),
            new DesktopRectangle(100, 50, 800, 600));

        Assert.Equal(108, position.Left);
        Assert.Equal(58, position.Top);
    }
}
