using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Microsoft.Win32;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

public sealed class WindowsMediaApplicationLauncher : IMediaApplicationLauncher
{
    private readonly IShortcutInputBackend _foregroundBackend;
    private readonly HashSet<string> _protectedProcesses;
    private readonly Func<string, bool> _isRunning;
    private readonly Func<string, string?> _resolveExecutable;
    private readonly Func<string, bool> _startExecutable;

    public WindowsMediaApplicationLauncher(
        IShortcutInputBackend foregroundBackend,
        SafetyPreferences safetyPreferences)
        : this(
            foregroundBackend,
            safetyPreferences,
            IsProcessRunning,
            ResolveExecutable,
            StartExecutable)
    {
    }

    internal WindowsMediaApplicationLauncher(
        IShortcutInputBackend foregroundBackend,
        SafetyPreferences safetyPreferences,
        Func<string, bool> isRunning,
        Func<string, string?> resolveExecutable,
        Func<string, bool> startExecutable)
    {
        ArgumentNullException.ThrowIfNull(foregroundBackend);
        ArgumentNullException.ThrowIfNull(safetyPreferences);
        ArgumentNullException.ThrowIfNull(isRunning);
        ArgumentNullException.ThrowIfNull(resolveExecutable);
        ArgumentNullException.ThrowIfNull(startExecutable);

        _foregroundBackend = foregroundBackend;
        _protectedProcesses = (safetyPreferences.ProtectedForegroundProcessNames ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeProcessName)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _isRunning = isRunning;
        _resolveExecutable = resolveExecutable;
        _startExecutable = startExecutable;
    }

    public bool IsRunning(string processName)
    {
        string normalized = NormalizeProcessName(processName);
        if (normalized.Length == 0)
        {
            return false;
        }

        try
        {
            return _isRunning(normalized);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
            Win32Exception or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public MediaApplicationLaunchResult TryLaunch(string processName)
    {
        string normalized = NormalizeProcessName(processName);
        if (normalized.Length == 0)
        {
            return new(MediaApplicationLaunchStatus.NotFound);
        }

        bool alreadyRunning;
        try
        {
            alreadyRunning = _isRunning(normalized);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
            Win32Exception or UnauthorizedAccessException)
        {
            return new(MediaApplicationLaunchStatus.SystemRejected);
        }

        if (alreadyRunning)
        {
            return new(MediaApplicationLaunchStatus.AlreadyRunning);
        }

        ForegroundProcessQuery foreground = _foregroundBackend.QueryForegroundProcess();
        if (!foreground.Succeeded)
        {
            return new(MediaApplicationLaunchStatus.ForegroundCheckUnavailable);
        }

        if (foreground.ProcessName is string foregroundProcess &&
            _protectedProcesses.Contains(NormalizeProcessName(foregroundProcess)))
        {
            return new(MediaApplicationLaunchStatus.ProtectedApplicationForeground);
        }

        string? executablePath;
        try
        {
            executablePath = _resolveExecutable(normalized + ".exe");
        }
        catch (Exception exception) when (
            exception is IOException or SecurityException or UnauthorizedAccessException)
        {
            return new(MediaApplicationLaunchStatus.SystemRejected);
        }
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new(MediaApplicationLaunchStatus.NotFound);
        }

        try
        {
            return new(_startExecutable(executablePath)
                ? MediaApplicationLaunchStatus.Started
                : MediaApplicationLaunchStatus.SystemRejected);
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        {
            return new(MediaApplicationLaunchStatus.SystemRejected);
        }
    }

    private static bool IsProcessRunning(string normalizedProcessName)
    {
        try
        {
            Process[] processes = Process.GetProcessesByName(normalizedProcessName);
            try
            {
                List<nint> mainWindowHandles = new(processes.Length);
                foreach (Process process in processes)
                {
                    try
                    {
                        process.Refresh();
                        mainWindowHandles.Add(process.MainWindowHandle);
                    }
                    catch (Exception exception) when (
                        exception is InvalidOperationException or NotSupportedException or
                        Win32Exception)
                    {
                        mainWindowHandles.Add(nint.Zero);
                    }
                }

                return HasControllableInstance(mainWindowHandles);
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    internal static bool HasControllableInstance(IEnumerable<nint> mainWindowHandles)
    {
        ArgumentNullException.ThrowIfNull(mainWindowHandles);
        return mainWindowHandles.Any(handle => handle != nint.Zero);
    }

    private static string? ResolveExecutable(string executableName)
    {
        string[] registryKeys =
        [
            $@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\{executableName}",
            $@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\{executableName}",
            $@"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\{executableName}",
        ];
        foreach (string key in registryKeys)
        {
            if (Registry.GetValue(key, null, null) is string registeredPath &&
                TryNormalizeExistingPath(registeredPath, out string? resolvedPath))
            {
                return resolvedPath;
            }
        }

        if (!executableName.Equals("cloudmusic.exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] candidates =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Netease",
                "CloudMusic",
                executableName),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Netease",
                "CloudMusic",
                executableName),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Netease",
                "CloudMusic",
                executableName),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static bool TryNormalizeExistingPath(string value, out string? path)
    {
        path = value.Trim().Trim('"');
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
        {
            path = null;
            return false;
        }

        path = Path.GetFullPath(path);
        return true;
    }

    private static bool StartExecutable(string executablePath)
    {
        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
            UseShellExecute = true,
        });
        return process is not null;
    }

    private static string NormalizeProcessName(string processName) =>
        Path.GetFileNameWithoutExtension(processName?.Trim() ?? string.Empty);
}
