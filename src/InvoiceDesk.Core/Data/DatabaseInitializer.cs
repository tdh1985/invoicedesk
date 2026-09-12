using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Data;

public sealed class DatabaseInitializer(
    AppPaths paths, IDbContextFactory<AppDbContext> factory, AttachmentStore store, TimeProvider clock)
{
    public const int BackupsToKeep = 10;

    static readonly string[] IncomeCategories = ["Sales", "Interest", "Other income"];
    static readonly string[] ExpenseCategories =
    [
        "Software & subscriptions", "Equipment", "Fuel & travel", "Office", "Phone & internet",
        "Advertising", "Professional fees", "Bank fees", "Other expense",
    ];

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        paths.EnsureCreated();
        BackupDatabase();
        store.ClearStaging();

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
        await SeedAsync(db, ct);
    }

    void BackupDatabase()
    {
        if (!File.Exists(paths.Database)) return;

        var target = Path.Combine(paths.Backups, $"invoicedesk-{clock.GetLocalNow():yyyyMMdd-HHmmss}.db");
        // sqlite's backup api gives a consistent copy even if a connection is open
        using (var source = new SqliteConnection($"Data Source={paths.Database};Pooling=False"))
        using (var destination = new SqliteConnection($"Data Source={target};Pooling=False"))
        {
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        }

        var stale = Directory.GetFiles(paths.Backups, "invoicedesk-*.db")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .Skip(BackupsToKeep);
        foreach (var old in stale) File.Delete(old);
    }

    static async Task SeedAsync(AppDbContext db, CancellationToken ct)
    {
        if (!await db.Profiles.AnyAsync(ct)) db.Profiles.Add(new BusinessProfile());

        if (!await db.Categories.AnyAsync(ct))
        {
            db.Categories.AddRange(IncomeCategories.Select((n, i) => new Category { Name = n, Direction = Direction.In, SortOrder = i }));
            db.Categories.AddRange(ExpenseCategories.Select((n, i) => new Category { Name = n, Direction = Direction.Out, SortOrder = i }));
        }

        await db.SaveChangesAsync(ct);
    }
}
