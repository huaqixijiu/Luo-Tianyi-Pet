using System.Runtime.InteropServices;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

public sealed class WindowsUserIdleTimeSource : IUserIdleTimeSource
{
    public TimeSpan? GetIdleDuration()
    {
        LastInputInfo input = new() { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref input))
        {
            return null;
        }

        uint elapsed = unchecked((uint)Environment.TickCount - input.TickCount);
        return TimeSpan.FromMilliseconds(elapsed);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo input);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint TickCount;
    }
}
