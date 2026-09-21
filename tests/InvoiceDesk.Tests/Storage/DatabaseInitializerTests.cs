// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Tests.Storage;

public class DatabaseInitializerTests
{
    [Fact]
    public async Task seeds_profile_and_categories_once()
    {
        await using var env = await TestEnv.CreateAsync();
        await env.Get<DatabaseInitializer>().InitializeAsync();

        await using var db = await env.Get<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await db.Profiles.CountAsync());
        Assert.Equal(12, await db.Categories.CountAsync());
        Assert.Equal(3, await db.Categories.CountAsync(c => c.Direction == Direction.In));
    }

    [Fact]
    public async Task backs_up_existing_db_and_keeps_ten()
    {
        await using var env = await TestEnv.CreateAsync();
        var init = env.Get<DatabaseInitializer>();
        for (var i = 0; i < 12; i++)
        {
            env.Clock.Advance(TimeSpan.FromMinutes(1));
            await init.InitializeAsync();
        }

        Assert.Equal(10, Directory.GetFiles(env.Paths.Backups, "*.db").Length);
    }

    [Fact]
    public async Task backup_is_a_readable_copy()
    {
        await using var env = await TestEnv.CreateAsync();
        env.Clock.Advance(TimeSpan.FromMinutes(1));
        await env.Get<DatabaseInitializer>().InitializeAsync();

        var backup = Directory.GetFiles(env.Paths.Backups, "*.db").Single();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={backup};Pooling=False").Options;
        await using var db = new AppDbContext(options);
        Assert.Equal(1, await db.Profiles.CountAsync());
    }

    [Fact]
    public async Task clears_staging_on_start()
    {
        await using var env = await TestEnv.CreateAsync();
        File.WriteAllText(Path.Combine(env.Paths.Staging, "old.pdf"), "x");

        await env.Get<DatabaseInitializer>().InitializeAsync();

        Assert.Empty(Directory.GetFiles(env.Paths.Staging));
    }
}
