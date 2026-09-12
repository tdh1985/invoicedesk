using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using InvoiceDesk.App.Host;
using InvoiceDesk.App.Ui;
using InvoiceDesk.Core;
using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace InvoiceDesk.App;

public partial class App : Application
{
    const string WebViewDownload = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    SingleInstance? _instance;
    ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = AppPaths.Default();
        _instance = new SingleInstance(paths.Root);
        if (!_instance.IsFirst)
        {
            _instance.SignalFirst();
            Shutdown();
            return;
        }

        paths.EnsureCreated();
        FileLog.Initialise(paths.Logs);
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => FileLog.Write(args.ExceptionObject as Exception, "unhandled");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            FileLog.Write(args.Exception, "unobserved task");
            args.SetObserved();
        };

        if (!WebViewRuntimeInstalled())
        {
            var install = MessageBox.Show(
                "InvoiceDesk needs the Microsoft Edge WebView2 Runtime, which isn't installed on this PC.\n\nOpen the download page now?",
                "InvoiceDesk", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (install == MessageBoxResult.Yes) Process.Start(new ProcessStartInfo(WebViewDownload) { UseShellExecute = true });
            Shutdown(1);
            return;
        }

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        services.AddLogging();
        services.AddInvoiceDeskCore(paths);
        services.AddInvoiceDeskUi();
        _services = services.BuildServiceProvider();

        try
        {
            await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        }
        catch (Exception ex)
        {
            FileLog.Write(ex, "startup");
            MessageBox.Show(
                $"InvoiceDesk couldn't open its data folder.\n\n{ex.Message}\n\nDetails were saved in {paths.Logs}.",
                "InvoiceDesk", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var window = new MainWindow(_services);
        MainWindow = window;
        _instance.ListenForActivation(() => Dispatcher.BeginInvoke(window.BringToFront));
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        try { _services?.Dispose(); }
        catch (Exception ex) { FileLog.Write(ex, "shutdown"); }
        base.OnExit(e);
    }

    static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        FileLog.Write(args.Exception, "dispatcher");
        args.Handled = true;
        MessageBox.Show(
            $"Something went wrong: {args.Exception.Message}\n\nYour data is safe. Details were saved in {FileLog.Folder}.",
            "InvoiceDesk", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    static bool WebViewRuntimeInstalled()
    {
        try { return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); }
        catch (WebView2RuntimeNotFoundException) { return false; }
    }
}
