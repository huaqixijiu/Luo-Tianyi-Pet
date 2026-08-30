using System.Diagnostics;
using System.Runtime.InteropServices;
using LuoTianyiPet.Core;
using NAudio.CoreAudioApi;

namespace LuoTianyiPet.Platform.Windows;

public interface ISystemVolumeBackend : IDisposable
{
    event EventHandler<SystemVolumeChangedEventArgs>? VolumeChanged;

    ForegroundProcessQuery QueryForegroundProcess();

    SystemVolumeSnapshot Read();

    bool TrySetLevel(float level, out SystemVolumeSnapshot snapshot);
}

public sealed class WindowsSystemVolumeService : ISystemVolumeService
{
    private readonly ISystemVolumeBackend _backend;
    private readonly bool _mouseWheelControlEnabled;
    private readonly float _step;
    private readonly HashSet<string> _protectedProcesses;
    private bool _disposed;

    public WindowsSystemVolumeService(
        ISystemVolumeBackend backend,
        VolumePreferences volumePreferences,
        SafetyPreferences safetyPreferences)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(volumePreferences);
        ArgumentNullException.ThrowIfNull(safetyPreferences);

        _backend = backend;
        _mouseWheelControlEnabled = volumePreferences.EnableMouseWheelControl;
        int stepPercent = volumePreferences.MouseWheelStepPercent is >= 1 and <= 20
            ? volumePreferences.MouseWheelStepPercent
            : VolumePreferences.DefaultMouseWheelStepPercent;
        _step = stepPercent / 100f;
        _protectedProcesses = (safetyPreferences.ProtectedForegroundProcessNames ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _backend.VolumeChanged += OnBackendVolumeChanged;
    }

    public event EventHandler<SystemVolumeChangedEventArgs>? VolumeChanged;

    public SystemVolumeSnapshot Read() => _backend.Read();

    public SystemVolumeSafetyStatus CheckFeedbackSafety()
    {
        ForegroundProcessQuery foreground = _backend.QueryForegroundProcess();
        if (!foreground.Succeeded)
        {
            return SystemVolumeSafetyStatus.ForegroundCheckUnavailable;
        }

        return foreground.ProcessName is string processName &&
            _protectedProcesses.Contains(NormalizeProcessName(processName))
                ? SystemVolumeSafetyStatus.ProtectedApplicationForeground
                : SystemVolumeSafetyStatus.Allowed;
    }

    public SystemVolumeAdjustmentResult TryAdjustBySteps(int steps)
    {
        if (!_mouseWheelControlEnabled)
        {
            return new(SystemVolumeAdjustmentStatus.Disabled, SystemVolumeSnapshot.Unavailable);
        }

        SystemVolumeSafetyStatus safety = CheckFeedbackSafety();
        if (safety != SystemVolumeSafetyStatus.Allowed)
        {
            return new(
                safety == SystemVolumeSafetyStatus.ProtectedApplicationForeground
                    ? SystemVolumeAdjustmentStatus.ProtectedApplicationForeground
                    : SystemVolumeAdjustmentStatus.ForegroundCheckUnavailable,
                SystemVolumeSnapshot.Unavailable);
        }

        SystemVolumeSnapshot current = _backend.Read();
        if (!current.IsAvailable)
        {
            return new(SystemVolumeAdjustmentStatus.EndpointUnavailable, current);
        }

        if (steps == 0)
        {
            return new(SystemVolumeAdjustmentStatus.AtLimit, current);
        }

        float target = Math.Clamp(current.Level + (steps * _step), 0, 1);
        if (Math.Abs(target - current.Level) < 0.0005f)
        {
            return new(SystemVolumeAdjustmentStatus.AtLimit, current);
        }

        if (!_backend.TrySetLevel(target, out SystemVolumeSnapshot adjusted) || !adjusted.IsAvailable)
        {
            return new(SystemVolumeAdjustmentStatus.SystemRejected, current);
        }

        return new(SystemVolumeAdjustmentStatus.Succeeded, adjusted);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _backend.VolumeChanged -= OnBackendVolumeChanged;
        _backend.Dispose();
    }

    private void OnBackendVolumeChanged(object? sender, SystemVolumeChangedEventArgs e)
    {
        if (!_disposed)
        {
            VolumeChanged?.Invoke(this, e);
        }
    }

    private static string NormalizeProcessName(string processName) =>
        Path.GetFileNameWithoutExtension(processName.Trim());
}

public sealed class CoreAudioSystemVolumeBackend : ISystemVolumeBackend
{
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private readonly MMDevice _device;
    private readonly AudioEndpointVolume _endpointVolume;
    private bool _disposed;

    public CoreAudioSystemVolumeBackend()
    {
        _deviceEnumerator = new MMDeviceEnumerator();
        try
        {
            _device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _endpointVolume = _device.AudioEndpointVolume;
            _endpointVolume.OnVolumeNotification += OnVolumeNotification;
        }
        catch
        {
            _deviceEnumerator.Dispose();
            throw;
        }
    }

    public event EventHandler<SystemVolumeChangedEventArgs>? VolumeChanged;

    public ForegroundProcessQuery QueryForegroundProcess()
    {
        nint window = NativeMethods.GetForegroundWindow();
        if (window == 0)
        {
            return new(true, null);
        }

        if (NativeMethods.GetWindowThreadProcessId(window, out uint processId) == 0 || processId == 0)
        {
            return new(false, null);
        }

        try
        {
            using Process process = Process.GetProcessById(checked((int)processId));
            return new(true, process.ProcessName);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new(false, null);
        }
    }

    public SystemVolumeSnapshot Read()
    {
        try
        {
            return SystemVolumeSnapshot.Available(
                _endpointVolume.MasterVolumeLevelScalar,
                _endpointVolume.Mute);
        }
        catch (Exception exception) when (
            exception is COMException or InvalidOperationException or ObjectDisposedException or UnauthorizedAccessException)
        {
            return SystemVolumeSnapshot.Unavailable;
        }
    }

    public bool TrySetLevel(float level, out SystemVolumeSnapshot snapshot)
    {
        try
        {
            _endpointVolume.MasterVolumeLevelScalar = Math.Clamp(level, 0, 1);
            snapshot = Read();
            return snapshot.IsAvailable;
        }
        catch (Exception exception) when (
            exception is COMException or InvalidOperationException or ObjectDisposedException or UnauthorizedAccessException)
        {
            snapshot = SystemVolumeSnapshot.Unavailable;
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _endpointVolume.OnVolumeNotification -= OnVolumeNotification;
        _endpointVolume.Dispose();
        _device.Dispose();
        _deviceEnumerator.Dispose();
    }

    private void OnVolumeNotification(AudioVolumeNotificationData data)
    {
        if (!_disposed)
        {
            VolumeChanged?.Invoke(
                this,
                new SystemVolumeChangedEventArgs(
                    SystemVolumeSnapshot.Available(data.MasterVolume, data.Muted)));
        }
    }
}
