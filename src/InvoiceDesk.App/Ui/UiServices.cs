// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.App.Host;
using InvoiceDesk.App.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceDesk.App.Ui;

public static class UiServices
{
    public static IServiceCollection AddInvoiceDeskUi(this IServiceCollection services)
    {
        services.AddSingleton<PrefsStore>();
        services.AddSingleton<Desktop>();
        services.AddSingleton<HostWindow>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<ShortcutService>();
        services.AddSingleton<PdfPrinter>();
        services.AddSingleton<InvoicePdfExporter>();
        return services;
    }
}
