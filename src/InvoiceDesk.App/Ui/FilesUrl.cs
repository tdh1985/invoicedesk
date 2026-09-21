// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Storage;
using Microsoft.Web.WebView2.Core;

namespace InvoiceDesk.App.Ui;

// made-up hosts mapped onto folders let the webview show receipts and logos
public static class FilesUrl
{
    public const string Host = "files.invoicedesk.example";

    // staging and render files stay on this pc, apart from the synced data
    public const string LocalHost = "local.invoicedesk.example";

    public static string For(string relativePath) => $"https://{Host}/{relativePath.TrimStart('/')}";

    public static string ForLocal(string relativePath) => $"https://{LocalHost}/{relativePath.TrimStart('/')}";

    public static string? Logo(BusinessProfile? profile) =>
        profile?.LogoAttachment is { } logo ? For(logo.StoredPath) : null;

    // the pdf webview needs the same hosts or printed invoices lose their logo
    public static void MapHosts(CoreWebView2 core, AppPaths paths)
    {
        core.SetVirtualHostNameToFolderMapping(Host, paths.DataRoot, CoreWebView2HostResourceAccessKind.Allow);
        core.SetVirtualHostNameToFolderMapping(LocalHost, paths.LocalRoot, CoreWebView2HostResourceAccessKind.Allow);
    }
}
