using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LuoTianyiPet.App;

public sealed class TrayIconController : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _topmostItem;
    private readonly Forms.ToolStripMenuItem _startupItem;
    private readonly Func<bool> _isTopmostEnabled;
    private readonly Func<bool> _isStartupEnabled;
    private bool _disposed;

    public TrayIconController(
        Action openSettings,
        Func<bool> isTopmostEnabled,
        Action<bool> setTopmostEnabled,
        Func<bool> isStartupEnabled,
        Action<bool> setStartupEnabled,
        Action exit)
    {
        ArgumentNullException.ThrowIfNull(openSettings);
        ArgumentNullException.ThrowIfNull(isTopmostEnabled);
        ArgumentNullException.ThrowIfNull(setTopmostEnabled);
        ArgumentNullException.ThrowIfNull(isStartupEnabled);
        ArgumentNullException.ThrowIfNull(setStartupEnabled);
        ArgumentNullException.ThrowIfNull(exit);

        _isTopmostEnabled = isTopmostEnabled;
        _isStartupEnabled = isStartupEnabled;
        Forms.ContextMenuStrip menu = new();
        Forms.ToolStripMenuItem settingsItem = new("打开设置");
        settingsItem.Click += (_, _) => openSettings();
        _topmostItem = new Forms.ToolStripMenuItem("始终置顶") { CheckOnClick = true };
        _topmostItem.Click += (_, _) => setTopmostEnabled(_topmostItem.Checked);
        _startupItem = new Forms.ToolStripMenuItem("开机自启动") { CheckOnClick = true };
        _startupItem.Click += (_, _) => setStartupEnabled(_startupItem.Checked);
        Forms.ToolStripMenuItem exitItem = new("退出桌宠");
        exitItem.Click += (_, _) => exit();
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_topmostItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => RefreshChecks();

        Drawing.Icon icon = ExtractApplicationIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "洛天依桌宠",
            Icon = icon,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                openSettings();
            }
        };
        RefreshChecks();
    }

    public void RefreshChecks()
    {
        _topmostItem.Checked = _isTopmostEnabled();
        _startupItem.Checked = _isStartupEnabled();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        Drawing.Icon? icon = _notifyIcon.Icon;
        Forms.ContextMenuStrip? menu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.Dispose();
        menu?.Dispose();
        icon?.Dispose();
    }

    private static Drawing.Icon ExtractApplicationIcon()
    {
        string? executablePath = Environment.ProcessPath;
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
