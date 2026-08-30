using LuoTianyiPet.Core;
using Microsoft.Win32;

namespace LuoTianyiPet.Platform.Windows;

public sealed class WindowsSystemResumeSource : ISystemResumeSource
{
    private bool _started;
    private bool _disposed;

    public event EventHandler<SystemResumeEventArgs>? Resumed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _started = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_started)
        {
            return;
        }

        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _started = false;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (!_disposed && e.Reason == SessionSwitchReason.SessionUnlock)
        {
            Resumed?.Invoke(
                this,
                new SystemResumeEventArgs(SystemResumeReason.SessionUnlocked, DateTimeOffset.Now));
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (!_disposed && e.Mode == PowerModes.Resume)
        {
            Resumed?.Invoke(
                this,
                new SystemResumeEventArgs(SystemResumeReason.PowerResumed, DateTimeOffset.Now));
        }
    }
}
