using System.Diagnostics;
using System.Runtime.InteropServices;
using LuoTianyiPet.Core;
using NAudio.CoreAudioApi;

namespace LuoTianyiPet.Platform.Windows;

/// <summary>
/// Reads and writes only the public Core Audio sessions owned by one process name.
/// This is the same session-volume layer surfaced by the Windows 11 volume mixer;
/// it never reads or writes the endpoint master volume.
/// </summary>
public sealed class CoreAudioApplicationVolumeService : IApplicationVolumeService
{
    private readonly string _processName;
    private readonly HashSet<string> _protectedProcesses;

    public CoreAudioApplicationVolumeService(
        string processName,
        SafetyPreferences safetyPreferences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        ArgumentNullException.ThrowIfNull(safetyPreferences);
        _processName = NormalizeProcessName(processName);
        _protectedProcesses = (safetyPreferences.ProtectedForegroundProcessNames ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public ApplicationVolumeSnapshot Read()
    {
        try
        {
            HashSet<uint> processIds = GetTargetProcessIds();
            if (processIds.Count == 0)
            {
                return ApplicationVolumeSnapshot.Missing;
            }

            List<float> levels = ReadMatchingSessionLevels(processIds);
            return levels.Count == 0
                ? ApplicationVolumeSnapshot.Missing
                : ApplicationVolumeSnapshot.Found(levels.Average(), levels.Count);
        }
        catch (Exception exception) when (IsExpectedAudioFailure(exception))
        {
            return ApplicationVolumeSnapshot.Unavailable;
        }
    }

    public ApplicationVolumeAdjustmentResult TrySetLevel(float level)
    {
        if (!float.IsFinite(level))
        {
            return new(
                ApplicationVolumeAdjustmentStatus.SystemRejected,
                ApplicationVolumeSnapshot.Unavailable);
        }

        ApplicationVolumeAdjustmentStatus? safetyFailure = GetSafetyFailure();
        if (safetyFailure is ApplicationVolumeAdjustmentStatus blocked)
        {
            return new(blocked, ApplicationVolumeSnapshot.Unavailable);
        }

        float target = Math.Clamp(level, 0, 1);
        ApplicationVolumeSnapshot before = Read();
        if (!before.ProbeSucceeded)
        {
            return new(ApplicationVolumeAdjustmentStatus.SessionUnavailable, before);
        }
        if (!before.TargetSessionFound)
        {
            return new(ApplicationVolumeAdjustmentStatus.TargetSessionMissing, before);
        }
        if (Math.Abs(before.Level - target) < 0.0005f)
        {
            return new(ApplicationVolumeAdjustmentStatus.AtLimit, before);
        }

        try
        {
            HashSet<uint> processIds = GetTargetProcessIds();
            int adjustedCount = SetMatchingSessionLevels(processIds, target);
            if (adjustedCount == 0)
            {
                return new(ApplicationVolumeAdjustmentStatus.TargetSessionMissing, before);
            }

            return new(
                ApplicationVolumeAdjustmentStatus.Succeeded,
                ApplicationVolumeSnapshot.Found(target, adjustedCount));
        }
        catch (Exception exception) when (IsExpectedAudioFailure(exception))
        {
            return new(ApplicationVolumeAdjustmentStatus.SystemRejected, before);
        }
    }

    public void Dispose()
    {
    }

    private List<float> ReadMatchingSessionLevels(HashSet<uint> processIds)
    {
        List<float> levels = [];
        using MMDeviceEnumerator deviceEnumerator = new();
        using MMDeviceCollection devices = deviceEnumerator.EnumerateAudioEndPoints(
            DataFlow.Render,
            DeviceState.Active);
        for (int deviceIndex = 0; deviceIndex < devices.Count; deviceIndex++)
        {
            using MMDevice device = devices[deviceIndex];
            using AudioSessionManager sessionManager = device.AudioSessionManager;
            using SessionCollection sessions = sessionManager.Sessions;
            for (int sessionIndex = 0; sessionIndex < sessions.Count; sessionIndex++)
            {
                using AudioSessionControl session = sessions[sessionIndex];
                if (!processIds.Contains(session.GetProcessID))
                {
                    continue;
                }

                using SimpleAudioVolume volume = session.SimpleAudioVolume;
                levels.Add(Math.Clamp(volume.Volume, 0, 1));
            }
        }

        return levels;
    }

    private int SetMatchingSessionLevels(HashSet<uint> processIds, float target)
    {
        int adjustedCount = 0;
        using MMDeviceEnumerator deviceEnumerator = new();
        using MMDeviceCollection devices = deviceEnumerator.EnumerateAudioEndPoints(
            DataFlow.Render,
            DeviceState.Active);
        for (int deviceIndex = 0; deviceIndex < devices.Count; deviceIndex++)
        {
            using MMDevice device = devices[deviceIndex];
            using AudioSessionManager sessionManager = device.AudioSessionManager;
            using SessionCollection sessions = sessionManager.Sessions;
            for (int sessionIndex = 0; sessionIndex < sessions.Count; sessionIndex++)
            {
                using AudioSessionControl session = sessions[sessionIndex];
                if (!processIds.Contains(session.GetProcessID))
                {
                    continue;
                }

                using SimpleAudioVolume volume = session.SimpleAudioVolume;
                volume.Volume = target;
                adjustedCount++;
            }
        }

        return adjustedCount;
    }

    private ApplicationVolumeAdjustmentStatus? GetSafetyFailure()
    {
        nint window = NativeMethods.GetForegroundWindow();
        if (window == nint.Zero ||
            NativeMethods.GetWindowThreadProcessId(window, out uint processId) == 0 ||
            processId == 0)
        {
            return ApplicationVolumeAdjustmentStatus.ForegroundCheckUnavailable;
        }

        try
        {
            using Process process = Process.GetProcessById(checked((int)processId));
            return _protectedProcesses.Contains(NormalizeProcessName(process.ProcessName))
                ? ApplicationVolumeAdjustmentStatus.ProtectedApplicationForeground
                : null;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
            System.ComponentModel.Win32Exception or OverflowException)
        {
            return ApplicationVolumeAdjustmentStatus.ForegroundCheckUnavailable;
        }
    }

    private HashSet<uint> GetTargetProcessIds()
    {
        HashSet<uint> processIds = [];
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(_processName);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or PlatformNotSupportedException)
        {
            return processIds;
        }

        foreach (Process process in processes)
        {
            using (process)
            {
                try
                {
                    processIds.Add(checked((uint)process.Id));
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or OverflowException)
                {
                    // The process exited while its session was being discovered.
                }
            }
        }

        return processIds;
    }

    private static string NormalizeProcessName(string processName) =>
        Path.GetFileNameWithoutExtension(processName.Trim());

    private static bool IsExpectedAudioFailure(Exception exception) =>
        exception is COMException or InvalidOperationException or ObjectDisposedException or
        ArgumentOutOfRangeException or UnauthorizedAccessException;

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);
    }
}
