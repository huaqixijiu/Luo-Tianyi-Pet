using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MessageNotificationWindow : Window
{
    public MessageNotificationWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            nint hwnd = new WindowInteropHelper(this).Handle;
            // This surface is decorative: it never takes focus or intercepts mouse input.
            SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x08000000 | 0x80 | 0x20);
        };
    }

    public void ShowAt(MessageSidePosition position)
    {
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }
        SetWindowPos(new WindowInteropHelper(this).Handle, 0,
            (int)Math.Round(position.Left), (int)Math.Round(position.Top), 0, 0,
            0x0001 | 0x0004 | 0x0010);
        Opacity = 1;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(nint hwnd, int index, int value);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
}
