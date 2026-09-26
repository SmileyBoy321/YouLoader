using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YouLoader;

static class DarkTitleBar
{
    const int DwmwaUseImmersiveDarkMode = 20;

    /// <summary>Matches the Windows title bar to YouLoader's dark windows (Windows 10 20H1 and later).</summary>
    public static void Apply(Window window)
    {
        var enabled = 1;
        DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
