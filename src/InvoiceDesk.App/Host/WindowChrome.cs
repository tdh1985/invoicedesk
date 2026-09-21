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

public static class ThemeColours
{
    public static Color Desk(bool dark) => dark ? Color.FromRgb(0x10, 0x14, 0x1F) : Color.FromRgb(0xE9, 0xEC, 0xF0);

    public static Color Ink(bool dark) => dark ? Color.FromRgb(0xE6, 0xE9, 0xF1) : Color.FromRgb(0x18, 0x21, 0x3A);

    public static System.Drawing.Color DeskDrawing(bool dark)
    {
        var c = Desk(dark);
        return System.Drawing.Color.FromArgb(255, c.R, c.G, c.B);
    }
}
