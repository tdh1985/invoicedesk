using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using InvoiceDesk.App.Host;
using InvoiceDesk.App.Ui;
using InvoiceDesk.Core;
using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace InvoiceDesk.App;

public partial class App : Application
{
    const string WebViewDownload = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    SingleInstance? _instance;
    ServiceProvider? _services;
    DataLock? _lock;
    DispatcherTimer? _heartbeat;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = AppPaths.Default();
        // a synced folder can be missing for a moment, for example before onedrive signs in
        while (paths.IsCustomLocation && !DataLocation.HasData(paths.DataRoot))
        {
            var choice = MessageBox.Show(
                $"InvoiceDesk keeps your data in:\n{paths.DataRoot}\n\nThat folder or its database can't be found right now. If it's in OneDrive, Dropbox or Google Drive, check that app is running and has finished syncing.\n\nYes: try again\nNo: switch back to this PC's own data\nCancel: close InvoiceDesk",
                "InvoiceDesk", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (choice == MessageBoxResult.No)
            {
                DataLocation.Save(paths.LocalRoot, null);
                paths = AppPaths.Default();
            }
            else if (choice != MessageBoxResult.Yes)
            {
                Shutdown();
                return;
            }
        }

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

        _lock = DataLock.ForThisProcess(paths, TimeProvider.System);
        if (_lock.ReadOther() is { } other && !ConfirmOpenElsewhere(other))
        {
            Shutdown();
            return;
        }
        _lock.Acquire();
        _heartbeat = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _heartbeat.Tick += (_, _) => _lock.Heartbeat();
        _heartbeat.Start();

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        services.AddLogging(logging => logging.AddProvider(new FileLogProvider()));
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
        _heartbeat?.Stop();
        _instance?.Dispose();
        try { _services?.Dispose(); }
        catch (Exception ex) { FileLog.Write(ex, "shutdown"); }
        // closed files let a sync app upload a complete database
        SqliteConnection.ClearAllPools();
        _lock?.Release();
        base.OnExit(e);
    }

    static bool ConfirmOpenElsewhere(LockInfo other)
    {
        var minutes = Math.Max(1, (int)Math.Round((DateTimeOffset.UtcNow - other.Heartbeat).TotalMinutes));
        var answer = MessageBox.Show(
            $"InvoiceDesk looks like it's open on {other.Machine} (active {minutes} min ago).\n\nUsing the same data on two PCs at once can lose changes. If you can, close it on {other.Machine} first.\n\nOpen it here anyway?",
            "InvoiceDesk", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return answer == MessageBoxResult.Yes;
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
