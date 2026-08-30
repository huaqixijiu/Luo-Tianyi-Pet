using LuoTianyiPet.Core;
using Microsoft.Win32;

namespace LuoTianyiPet.Platform.Windows;

public sealed class WindowsStartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LuoTianyiPet";
    private readonly string _command;

    public WindowsStartupRegistrationService(
        string executablePath,
        bool portable,
        string? packageFamilyName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!string.IsNullOrWhiteSpace(packageFamilyName))
        {
            _command = $"explorer.exe shell:AppsFolder\\{packageFamilyName}!App";
        }
        else
        {
            string portableArgument = portable ? " --portable" : string.Empty;
            _command = $"\"{Path.GetFullPath(executablePath)}\"{portableArgument}";
        }
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(ValueName) is string value &&
                    string.Equals(value, _command, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                return false;
            }
        }
    }

    public StartupRegistrationResult TrySetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
        {
            return new(StartupRegistrationStatus.Unchanged, enabled);
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return new(StartupRegistrationStatus.Unavailable, IsEnabled);
            }

            if (enabled)
            {
                key.SetValue(ValueName, _command, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            bool actual = IsEnabled;
            return new(
                actual == enabled ? StartupRegistrationStatus.Succeeded : StartupRegistrationStatus.Rejected,
                actual);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return new(StartupRegistrationStatus.Rejected, IsEnabled);
        }
    }
}
