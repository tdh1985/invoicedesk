// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Runtime.InteropServices;
using System.Windows.Media;

namespace InvoiceDesk.App.Host;

// tints the windows 11 title bar to match the desk so the chrome disappears
public static class WindowChrome
{
    const int UseImmersiveDarkMode = 20;
    const int CaptionColour = 35;
    const int TextColour = 36;

    public static void Apply(IntPtr hwnd, bool dark, Color caption, Color text)
    {
        var darkFlag = dark ? 1 : 0;
        var captionRef = ToColorRef(caption);
        var textRef = ToColorRef(text);
        _ = DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref darkFlag, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, CaptionColour, ref captionRef, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, TextColour, ref textRef, sizeof(int));
    }

    static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
