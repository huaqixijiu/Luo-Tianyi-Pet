using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public sealed class TrayIconController : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _topmostItem;
    private readonly Forms.ToolStripMenuItem _startupItem;
    private readonly Forms.ToolStripLabel _displayScaleLabel;
    private readonly Forms.TrackBar _displayScaleTrackBar;
    private readonly Func<bool> _isTopmostEnabled;
    private readonly Func<bool> _isStartupEnabled;
    private readonly Func<int> _getDisplayScalePercent;
    private readonly Action<int> _previewDisplayScalePercent;
    private readonly Action<int> _commitDisplayScalePercent;
    private bool _refreshingDisplayScale;
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
        ArgumentNullException.ThrowIfNull(openSettings);
        ArgumentNullException.ThrowIfNull(isTopmostEnabled);
        ArgumentNullException.ThrowIfNull(setTopmostEnabled);
        ArgumentNullException.ThrowIfNull(isStartupEnabled);
        ArgumentNullException.ThrowIfNull(setStartupEnabled);
        ArgumentNullException.ThrowIfNull(getDisplayScalePercent);
        ArgumentNullException.ThrowIfNull(previewDisplayScalePercent);
        ArgumentNullException.ThrowIfNull(commitDisplayScalePercent);
        ArgumentNullException.ThrowIfNull(exit);

        _isTopmostEnabled = isTopmostEnabled;
        _isStartupEnabled = isStartupEnabled;
        _getDisplayScalePercent = getDisplayScalePercent;
        _previewDisplayScalePercent = previewDisplayScalePercent;
        _commitDisplayScalePercent = commitDisplayScalePercent;
        Forms.ContextMenuStrip menu = new();
        Forms.ToolStripMenuItem settingsItem = new("打开设置");
        settingsItem.Click += (_, _) => openSettings();
        _topmostItem = new Forms.ToolStripMenuItem("始终置顶") { CheckOnClick = true };
        _topmostItem.Click += (_, _) => setTopmostEnabled(_topmostItem.Checked);
        _startupItem = new Forms.ToolStripMenuItem("开机自启动") { CheckOnClick = true };
        _startupItem.Click += (_, _) => setStartupEnabled(_startupItem.Checked);
        _displayScaleLabel = new Forms.ToolStripLabel("显示大小 100%")
        {
            Margin = new Forms.Padding(6, 5, 6, 0),
        };
        _displayScaleTrackBar = new Forms.TrackBar
        {
            Minimum = AppearancePreferences.MinimumDisplayScalePercent,
            Maximum = AppearancePreferences.MaximumDisplayScalePercent,
            TickFrequency = 25,
            SmallChange = 5,
            LargeChange = 10,
            Width = 190,
            Height = 34,
            AutoSize = false,
        };
        _displayScaleTrackBar.ValueChanged += (_, _) => PreviewDisplayScale();
        _displayScaleTrackBar.MouseUp += (_, _) => CommitDisplayScale();
        _displayScaleTrackBar.KeyUp += (_, _) => CommitDisplayScale();
        Forms.ToolStripControlHost displayScaleHost = new(_displayScaleTrackBar)
        {
            AutoSize = false,
            Width = 210,
            Height = 38,
            Margin = new Forms.Padding(4, 0, 4, 3),
        };
        Forms.ToolStripMenuItem exitItem = new("退出桌宠");
        exitItem.Click += (_, _) => exit();
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_topmostItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_displayScaleLabel);
        menu.Items.Add(displayScaleHost);
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
        int displayScale = Math.Clamp(
            _getDisplayScalePercent(),
            AppearancePreferences.MinimumDisplayScalePercent,
            AppearancePreferences.MaximumDisplayScalePercent);
        _refreshingDisplayScale = true;
        _displayScaleTrackBar.Value = displayScale;
        _displayScaleLabel.Text = $"显示大小 {displayScale}%";
        _refreshingDisplayScale = false;
    }

    private void PreviewDisplayScale()
    {
        if (_refreshingDisplayScale)
        {
            return;
        }

        int value = _displayScaleTrackBar.Value;
        _displayScaleLabel.Text = $"显示大小 {value}%";
        _previewDisplayScalePercent(value);
    }

    private void CommitDisplayScale()
    {
        if (!_refreshingDisplayScale)
        {
            _commitDisplayScalePercent(_displayScaleTrackBar.Value);
        }
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
