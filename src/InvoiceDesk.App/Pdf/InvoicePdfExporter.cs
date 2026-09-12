using System.IO;
using System.Net;
using InvoiceDesk.App.Host;
using InvoiceDesk.App.Ui;
using InvoiceDesk.App.Ui.Components;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.App.Pdf;

public sealed class InvoicePdfExporter(
    IServiceProvider services, InvoiceService invoices, ProfileService profiles,
    PdfPrinter printer, AppPaths paths, TimeProvider clock)
{
    public static string DefaultFileName(Invoice invoice) =>
        Format.SafeFileName($"{invoice.Number} - {invoice.Client?.Name}") + ".pdf";

    public async Task<string> ExportAsync(int invoiceId, string? targetPath = null)
    {
        var invoice = await invoices.GetAsync(invoiceId) ?? throw new ValidationException("This invoice no longer exists.");
        var profile = await profiles.GetAsync();
        var path = targetPath ?? Path.Combine(paths.Exports, DefaultFileName(invoice));
        var html = await RenderAsync(invoice, profile);
        await printer.PrintAsync(html, path);
        return path;
    }

    async Task<string> RenderAsync(Invoice invoice, BusinessProfile profile)
    {
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var body = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(InvoiceDocument.Invoice)] = invoice,
                [nameof(InvoiceDocument.Profile)] = profile,
                [nameof(InvoiceDocument.LogoUrl)] = profile.LogoAttachment is { } logo ? FilesUrl.For(logo.StoredPath) : null,
                [nameof(InvoiceDocument.Today)] = clock.Today(),
            });
            var output = await renderer.RenderComponentAsync<InvoiceDocument>(parameters);
            return output.ToHtmlString();
        });

        var css = EmbeddedAssets.Instance.ReadText("css/invoice.css");
        var title = WebUtility.HtmlEncode(invoice.Number);
        return $$"""
            <!DOCTYPE html>
            <html lang="en-AU">
            <head>
            <meta charset="utf-8">
            <title>{{title}}</title>
            <style>html, body { margin: 0; background: #fff; }{{css}}</style>
            </head>
            <body>{{body}}</body>
            </html>
            """;
    }
}
