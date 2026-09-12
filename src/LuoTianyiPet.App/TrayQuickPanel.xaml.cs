using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using LuoTianyiPet.Core;
using Forms = System.Windows.Forms;

namespace LuoTianyiPet.App;

public partial class TrayQuickPanel : Window
{
    private readonly Action _showPet;
    private readonly Action _hidePet;
    private readonly Func<bool> _isPetVisible;
    private readonly Action _openSettings;
    private readonly Action _exit;

    public TrayQuickPanel(Action showPet, Action hidePet, Func<bool> isPetVisible,
        Action openSettings, Action exit)
    {
        _showPet = showPet;
        _hidePet = hidePet;
        _isPetVisible = isPetVisible;
        _openSettings = openSettings;
        _exit = exit;
        InitializeComponent();
    }

    public void RefreshState() => HidePetButton.IsEnabled = _isPetVisible();

    public void ShowNearTray()
    {
        RefreshState();
        ResetExitConfirmation();
        Opacity = 0;
        Show();
        UpdateLayout();
        PositionNearCursor();
        Opacity = 1;
        Activate();
        ShowPetButton.Focus();
    }

    public void HidePanel() => Hide();

    private void PositionNearCursor()
    {
        System.Drawing.Point cursor = Forms.Cursor.Position;
        System.Drawing.Rectangle work = Forms.Screen.FromPoint(cursor).WorkingArea;
        nint monitor = MonitorFromPoint(new NativePoint(cursor.X, cursor.Y), 2);
        double scale = monitor != 0 && GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 && dpi > 0
            ? dpi / 96d : 1;
        TrayQuickPanelPosition position = TrayQuickPanelPlacement.Resolve(
            new PointerPoint(cursor.X, cursor.Y),
            new DesktopRectangle(0, 0, ActualWidth * scale, ActualHeight * scale),
            new DesktopRectangle(work.Left, work.Top, work.Width, work.Height));
        _ = SetWindowPos(new WindowInteropHelper(this).Handle, 0,
            (int)Math.Round(position.Left), (int)Math.Round(position.Top), 0, 0,
            0x0001 | 0x0004 | 0x0010);
    }

    private void OnShowPetClick(object sender, RoutedEventArgs e) { HidePanel(); _showPet(); }
    private void OnHidePetClick(object sender, RoutedEventArgs e) { HidePanel(); _hidePet(); }
    private void OnOpenSettingsClick(object sender, RoutedEventArgs e) { HidePanel(); _openSettings(); }
    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        ExitButton.Visibility = Visibility.Collapsed;
        ExitConfirmationPanel.Visibility = Visibility.Visible;
        UpdateLayout();
        PositionNearCursor();
    }
    private void OnCancelExitClick(object sender, RoutedEventArgs e)
    {
        ResetExitConfirmation();
        UpdateLayout();
        PositionNearCursor();
    }
    private void OnConfirmExitClick(object sender, RoutedEventArgs e) { HidePanel(); _exit(); }
    private void ResetExitConfirmation()
    {
        ExitButton.Visibility = Visibility.Visible;
        ExitConfirmationPanel.Visibility = Visibility.Collapsed;
    }
    private void OnDeactivated(object? sender, EventArgs e) => HidePanel();
    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { HidePanel(); e.Handled = true; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }
    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y,
        int width, int height, uint flags);
}
