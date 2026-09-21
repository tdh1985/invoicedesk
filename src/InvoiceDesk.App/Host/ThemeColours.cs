// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Windows.Media;

namespace InvoiceDesk.App.Host;

// kept in step with --desk and --ink in app.css so the frame blends in
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
