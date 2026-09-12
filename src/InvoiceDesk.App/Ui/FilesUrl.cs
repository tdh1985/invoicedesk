namespace InvoiceDesk.App.Ui;

// the webview maps this made-up host onto the data folder so receipts and logos can be shown
public static class FilesUrl
{
    public const string Host = "files.invoicedesk.example";

    public static string For(string relativePath) => $"https://{Host}/{relativePath.TrimStart('/')}";
}
