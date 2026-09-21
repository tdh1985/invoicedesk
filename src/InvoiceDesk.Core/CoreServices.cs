// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoiceDesk.Core;

public static class CoreServices
{
    public static IServiceCollection AddInvoiceDeskCore(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        // split queries so loading lines with payments never multiplies rows
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(
            $"Data Source={paths.Database};Foreign Keys=True",
            sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        services.AddSingleton<AttachmentStore>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<DataMover>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<ClientService>();
        services.AddSingleton<CategoryService>();
        services.AddSingleton<InvoiceService>();
        services.AddSingleton<TransactionService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<SearchService>();
        return services;
    }
}
