using System.Diagnostics;

namespace LuoTianyiPet.App;

internal static class ApplicationRuntime
{
    public static string? ExecutablePath
    {
        get
        {
            try
            {
                using Process process = Process.GetCurrentProcess();
                return process.MainModule?.FileName;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }
    }
}
