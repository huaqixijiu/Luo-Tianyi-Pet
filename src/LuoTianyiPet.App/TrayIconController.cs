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
        Action openSettings,
        Func<bool> isTopmostEnabled,
        Action<bool> setTopmostEnabled,
        Func<bool> isStartupEnabled,
        Action<bool> setStartupEnabled,
        Func<int> getDisplayScalePercent,
        Action<int> previewDisplayScalePercent,
        Action<int> commitDisplayScalePercent,
        Action exit)
    {
        Guard.NotNull(openSettings, nameof(openSettings));
        Guard.NotNull(isTopmostEnabled, nameof(isTopmostEnabled));
        Guard.NotNull(setTopmostEnabled, nameof(setTopmostEnabled));
        Guard.NotNull(isStartupEnabled, nameof(isStartupEnabled));
        Guard.NotNull(setStartupEnabled, nameof(setStartupEnabled));
        Guard.NotNull(getDisplayScalePercent, nameof(getDisplayScalePercent));
        Guard.NotNull(previewDisplayScalePercent, nameof(previewDisplayScalePercent));
        Guard.NotNull(commitDisplayScalePercent, nameof(commitDisplayScalePercent));
        Guard.NotNull(exit, nameof(exit));

        _quickPanel = new TrayQuickPanel(
            openSettings,
            isTopmostEnabled,
            setTopmostEnabled,
            isStartupEnabled,
            setStartupEnabled,
            getDisplayScalePercent,
            previewDisplayScalePercent,
            commitDisplayScalePercent,
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
                openSettings();
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
