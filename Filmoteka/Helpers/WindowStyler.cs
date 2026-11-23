using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Filmoteka.Helpers;

/// <summary>
/// Прави заглавната лента на прозореца тъмна (Windows 10/11), за да пасва на темата.
/// </summary>
internal static class WindowStyler
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeOld = 19;
    private const int DwmCaptionColor = 35;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyDarkTitleBar(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;

                int enabled = 1;
                if (DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkModeOld, ref enabled, sizeof(int));

                // Цвят на лентата #0E0F13 (форматът е 0x00BBGGRR). Работи само на Windows 11.
                int caption = 0x00130F0E;
                DwmSetWindowAttribute(hwnd, DwmCaptionColor, ref caption, sizeof(int));
            }
            catch
            {
                // По-стари версии на Windows – оставяме стандартната лента.
            }
        };
    }
}
