using System.ComponentModel;
using System.Diagnostics;

namespace LuoTianyiPet.Platform.Windows;

public static class WindowsWindowProcessResolver
{
    public static bool TryGetProcessName(nint windowHandle, out string? processName)
    {
        processName = null;
        if (windowHandle == 0 ||
            DesktopNativeMethods.GetWindowThreadProcessId(windowHandle, out uint processId) == 0 ||
            processId == 0)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById(checked((int)processId));
            processName = process.ProcessName;
            return !string.IsNullOrWhiteSpace(processName);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or Win32Exception or OverflowException)
        {
            return false;
        }
    }
}

public static class ShellAttentionMessageClassifier
{
    // HSHELL_FLASH is HSHELL_REDRAW with HSHELL_HIGHBIT set. RegisterShellHookWindow
    // delivers the target top-level HWND in lParam for this message.
    public const int FlashCode = 0x8006;

    public static bool TryGetFlashingWindow(nint wParam, nint lParam, out nint windowHandle)
    {
        windowHandle = 0;
        if (unchecked((int)(long)wParam) != FlashCode || lParam == 0)
        {
            return false;
        }

        windowHandle = lParam;
        return true;
    }
}
