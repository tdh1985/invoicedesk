using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using InvoiceDesk.App.Host;
using InvoiceDesk.App.Ui;
using InvoiceDesk.Core.Storage;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace InvoiceDesk.App;

public partial class MainWindow : Window
{
    static readonly string[] EditingMenuItems = ["cut", "copy", "paste", "pasteAndMatchStyle", "selectAll", "undo", "redo", "spellCheck"];

    readonly AppPaths _paths;
    readonly PrefsStore _prefs;
    readonly ThemeService _theme;
    readonly ToastService _toasts;
    IntPtr _hwnd;

    public MainWindow(IServiceProvider services)
    {
        _paths = services.GetRequiredService<AppPaths>();
        _prefs = services.GetRequiredService<PrefsStore>();
        _theme = services.GetRequiredService<ThemeService>();
        _toasts = services.GetRequiredService<ToastService>();

        Resources.Add("services", services);
        InitializeComponent();
        WindowPlacement.Restore(this, _prefs.Current);

        var dark = _theme.ResolveInitial();
        ApplyChrome(dark);
        WebView.BlazorWebViewInitializing += OnWebViewInitializing;
        WebView.BlazorWebViewInitialized += OnWebViewInitialized;
        _theme.ResolvedChanged += isDark => Dispatcher.BeginInvoke(() => ApplyChrome(isDark));

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            services.GetRequiredService<HostWindow>().Handle = _hwnd;
            ApplyChrome(_theme.IsDark);
        };
        Closing += OnClosing;
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    void OnWebViewInitializing(object? sender, BlazorWebViewInitializingEventArgs e)
    {
        e.UserDataFolder = _paths.WebView;
        var port = Environment.GetEnvironmentVariable("INVOICEDESK_CDP_PORT");
        if (!string.IsNullOrWhiteSpace(port))
            e.EnvironmentOptions = new CoreWebView2EnvironmentOptions($"--remote-debugging-port={port}");
    }

    void OnWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        var core = e.WebView.CoreWebView2;
        e.WebView.DefaultBackgroundColor = ThemeColours.DeskDrawing(_theme.IsDark);
        core.SetVirtualHostNameToFolderMapping(FilesUrl.Host, _paths.DataRoot, CoreWebView2HostResourceAccessKind.Allow);
        core.SetVirtualHostNameToFolderMapping(FilesUrl.LocalHost, _paths.LocalRoot, CoreWebView2HostResourceAccessKind.Allow);
        core.Settings.IsStatusBarEnabled = false;
#if !DEBUG
        // no reload, find or print shortcuts: this should feel like an app, not a browser tab
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.ContextMenuRequested += (_, args) =>
        {
            var keep = args.MenuItems.Where(i => EditingMenuItems.Contains(i.Name)).ToList();
            args.MenuItems.Clear();
            foreach (var item in keep) args.MenuItems.Add(item);
            if (keep.Count == 0) args.Handled = true;
        };
    }

    void ApplyChrome(bool dark)
    {
        Background = new SolidColorBrush(ThemeColours.Desk(dark));
        if (_hwnd != IntPtr.Zero) Host.WindowChrome.Apply(_hwnd, dark, ThemeColours.Desk(dark), ThemeColours.Ink(dark));
    }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        WindowPlacement.Save(this, _prefs.Current);
        _prefs.Save();
        // pending undo-able deletes are committed rather than silently dropped
        Task.Run(_toasts.FlushAsync).Wait(TimeSpan.FromSeconds(5));
    }
}
