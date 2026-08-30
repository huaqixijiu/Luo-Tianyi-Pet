using System.Diagnostics;
using System.Runtime.InteropServices;
using LuoTianyiPet.Core;
using NAudio.CoreAudioApi;

namespace LuoTianyiPet.Platform.Windows;

public sealed class CoreAudioSessionProbe : IAudioSessionProbe
{
    public AudioSessionSnapshot ReadForProcess(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        string baseProcessName = Path.GetFileNameWithoutExtension(processName);
        HashSet<uint> targetProcessIds = GetTargetProcessIds(baseProcessName);
        if (targetProcessIds.Count == 0)
        {
            return AudioSessionSnapshot.Missing;
        }

        try
        {
            using MMDeviceEnumerator deviceEnumerator = new();
            using MMDevice device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            using AudioSessionManager sessionManager = device.AudioSessionManager;
            using SessionCollection sessions = sessionManager.Sessions;
            bool found = false;
            float maximumPeak = 0;
            for (int index = 0; index < sessions.Count; index++)
            {
                using AudioSessionControl session = sessions[index];
                if (!targetProcessIds.Contains(session.GetProcessID))
                {
                    continue;
                }

                found = true;
                maximumPeak = Math.Max(maximumPeak, session.AudioMeterInformation.MasterPeakValue);
            }

            return found ? AudioSessionSnapshot.Found(maximumPeak) : AudioSessionSnapshot.Missing;
        }
        catch (Exception exception) when (
            exception is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            return AudioSessionSnapshot.Unavailable;
        }
    }

    private static HashSet<uint> GetTargetProcessIds(string processName)
    {
        HashSet<uint> processIds = [];
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception exception) when (exception is InvalidOperationException or PlatformNotSupportedException)
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
                    // The process exited between enumeration and reading its ID.
                }
            }
        }

        return processIds;
    }
}
