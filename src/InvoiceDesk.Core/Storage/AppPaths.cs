namespace InvoiceDesk.Core.Storage;

public sealed class AppPaths(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    public string Database => Path.Combine(Root, "invoicedesk.db");
    public string Attachments => Path.Combine(Root, "attachments");
    public string Exports => Path.Combine(Root, "exports");
    public string Backups => Path.Combine(Root, "backups");
    public string Logs => Path.Combine(Root, "logs");
    public string Staging => Path.Combine(Root, "staging");
    public string Render => Path.Combine(Root, "render");
    public string WebView => Path.Combine(Root, "webview");
    public string Prefs => Path.Combine(Root, "prefs.json");

    // INVOICEDESK_DATA lets a test run use a clean folder without touching real data
    public static AppPaths Default()
    {
        var custom = Environment.GetEnvironmentVariable("INVOICEDESK_DATA");
        return new AppPaths(string.IsNullOrWhiteSpace(custom)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvoiceDesk")
            : custom);
    }

    public void EnsureCreated()
    {
        foreach (var dir in new[] { Root, Attachments, Exports, Backups, Logs, Staging, Render, WebView })
            Directory.CreateDirectory(dir);
    }

    public string FullPath(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
