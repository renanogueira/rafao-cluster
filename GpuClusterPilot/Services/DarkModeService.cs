using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RafaoCluster.Services;

public static class DarkModeService
{
    private const int ImmersiveDarkMode = 20;
    private const int ImmersiveDarkModeLegacy = 19;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    public static void Apply(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).EnsureHandle();
            var enabled = 1;
            if (DwmSetWindowAttribute(handle, ImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, ImmersiveDarkModeLegacy, ref enabled, sizeof(int));

            var caption = ColorRef(11, 16, 32);
            var text = ColorRef(255, 255, 255);
            DwmSetWindowAttribute(handle, CaptionColor, ref caption, sizeof(int));
            DwmSetWindowAttribute(handle, TextColor, ref text, sizeof(int));
        }
        catch
        {
            // Older Windows versions can ignore unsupported DWM attributes safely.
        }
    }

    private static int ColorRef(byte red, byte green, byte blue) => red | green << 8 | blue << 16;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
