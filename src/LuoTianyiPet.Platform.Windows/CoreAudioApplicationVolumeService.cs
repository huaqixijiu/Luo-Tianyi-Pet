using System.Diagnostics;
using System.Runtime.InteropServices;
using LuoTianyiPet.Core;
using NAudio.CoreAudioApi;

namespace LuoTianyiPet.Platform.Windows;

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
        _processName = Path.GetFileNameWithoutExtension(processName.Trim());
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

            using MMDeviceEnumerator deviceEnumerator = new();
            using MMDevice device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            using AudioSessionManager sessionManager = device.AudioSessionManager;
            using SessionCollection sessions = sessionManager.Sessions;
            List<float> levels = [];
            for (int index = 0; index < sessions.Count; index++)
            {
                using AudioSessionControl session = sessions[index];
                if (!processIds.Contains(session.GetProcessID))
                {
                    continue;
                }

                using SimpleAudioVolume volume = session.SimpleAudioVolume;
                levels.Add(volume.Volume);
            }

            return levels.Count == 0
                ? ApplicationVolumeSnapshot.Missing
                : ApplicationVolumeSnapshot.Found(levels.Average());
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
            using MMDeviceEnumerator deviceEnumerator = new();
            using MMDevice device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            using AudioSessionManager sessionManager = device.AudioSessionManager;
            using SessionCollection sessions = sessionManager.Sessions;
            int adjustedCount = 0;
            for (int index = 0; index < sessions.Count; index++)
            {
                using AudioSessionControl session = sessions[index];
                if (!processIds.Contains(session.GetProcessID))
                {
                    continue;
                }

                using SimpleAudioVolume volume = session.SimpleAudioVolume;
                volume.Volume = target;
                adjustedCount++;
            }

            if (adjustedCount == 0)
            {
                return new(ApplicationVolumeAdjustmentStatus.TargetSessionMissing, before);
            }

            return new(
                ApplicationVolumeAdjustmentStatus.Succeeded,
                ApplicationVolumeSnapshot.Found(target));
        }
        catch (Exception exception) when (IsExpectedAudioFailure(exception))
        {
            return new(ApplicationVolumeAdjustmentStatus.SystemRejected, before);
        }
    }

    public void Dispose()
    {
    }

    private ApplicationVolumeAdjustmentStatus? GetSafetyFailure()
    {
        nint window = NativeMethods.GetForegroundWindow();
        if (window == 0)
        {
            return null;
        }
        if (NativeMethods.GetWindowThreadProcessId(window, out uint processId) == 0 || processId == 0)
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
            System.ComponentModel.Win32Exception)
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
                    processIds.Add((uint)process.Id);
                }
                catch (InvalidOperationException)
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
        UnauthorizedAccessException;
}
