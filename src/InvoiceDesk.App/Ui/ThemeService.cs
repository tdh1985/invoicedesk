// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.App.Host;
using Microsoft.Win32;

namespace InvoiceDesk.App.Ui;

public sealed class ThemeService(PrefsStore prefs)
{
    public static readonly string[] Choices = ["system", "light", "dark"];

    public string Theme => prefs.Current.Theme;
    public bool IsDark { get; private set; }

    public event Action? Changed;
    public event Action<bool>? ResolvedChanged;

    public void Set(string theme)
    {
        if (!Choices.Contains(theme) || theme == Theme) return;
        prefs.Current.Theme = theme;
        prefs.Save();
        Changed?.Invoke();
    }

    // the page reports what it actually painted, which settles "system"
    public void ReportResolved(bool dark)
    {
        if (dark == IsDark) return;
        IsDark = dark;
        ResolvedChanged?.Invoke(dark);
    }

    public bool ResolveInitial()
    {
        IsDark = Theme switch
        {
            "dark" => true,
            "light" => false,
            _ => WindowsUsesDarkApps(),
        };
        return IsDark;
    }

    static bool WindowsUsesDarkApps()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
    }
}
