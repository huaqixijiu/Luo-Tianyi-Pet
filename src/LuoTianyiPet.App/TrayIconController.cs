using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public sealed class TrayIconController : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly TrayQuickPanel _quickPanel;
    private bool _disposed;

    public TrayIconController(
        Action showPet,
        Action hidePet,
        Func<bool> isPetVisible,
        Action openSettings,
        Action exit)
    {
        Guard.NotNull(openSettings, nameof(openSettings));
        Guard.NotNull(showPet, nameof(showPet));
        Guard.NotNull(hidePet, nameof(hidePet));
        Guard.NotNull(isPetVisible, nameof(isPetVisible));
        Guard.NotNull(exit, nameof(exit));

        _quickPanel = new TrayQuickPanel(
            showPet,
            hidePet,
            isPetVisible,
            openSettings,
            exit);

        Drawing.Icon icon = ExtractApplicationIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "洛天依桌宠",
            Icon = icon,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                _quickPanel.HidePanel();
                showPet();
            }
            else if (eventArgs.Button == Forms.MouseButtons.Right)
            {
                ToggleQuickPanel();
            }
        };
        RefreshChecks();
    }

    public void RefreshChecks()
    {
        _quickPanel.RefreshState();
    }

    public void ShowQuickPanel()
    {
        _quickPanel.ShowNearTray();
    }

    private void ToggleQuickPanel()
    {
        if (_quickPanel.IsVisible)
        {
            _quickPanel.HidePanel();
            return;
        }

        ShowQuickPanel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _quickPanel.Close();
        _notifyIcon.Visible = false;
        Drawing.Icon? icon = _notifyIcon.Icon;
        _notifyIcon.Dispose();
        icon?.Dispose();
    }

    private static Drawing.Icon ExtractApplicationIcon()
    {
        string? executablePath = ApplicationRuntime.ExecutablePath;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            Drawing.Icon? extracted = Drawing.Icon.ExtractAssociatedIcon(executablePath);
            if (extracted is not null)
            {
                return (Drawing.Icon)extracted.Clone();
            }
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }
}
