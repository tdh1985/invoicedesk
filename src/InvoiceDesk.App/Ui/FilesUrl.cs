namespace InvoiceDesk.App.Ui;

// the webview maps made-up hosts onto folders so receipts, logos and previews can be shown
public static class FilesUrl
{
    public const string Host = "files.invoicedesk.example";

    // staging and render files stay on this pc, apart from the synced data
    public const string LocalHost = "local.invoicedesk.example";

    public static string For(string relativePath) => $"https://{Host}/{relativePath.TrimStart('/')}";

    public static string ForLocal(string relativePath) => $"https://{LocalHost}/{relativePath.TrimStart('/')}";
}
