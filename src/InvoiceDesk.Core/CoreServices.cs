using InvoiceDesk.Core.Data;
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
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={paths.Database}"));
        services.AddSingleton<AttachmentStore>();
        services.AddSingleton<DatabaseInitializer>();
        return services;
    }
}
