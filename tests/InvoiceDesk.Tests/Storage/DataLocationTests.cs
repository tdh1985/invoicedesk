using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Tests.Storage;

public sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InvoiceDeskTests", Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Path);

    public string Sub(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public class DataLocationTests
{
    [Fact]
    public void defaults_to_the_local_folder()
    {
        using var temp = new TempFolder();

        var paths = DataLocation.Resolve(temp.Path);

        Assert.Equal(temp.Path, paths.DataRoot);
        Assert.Equal(temp.Path, paths.LocalRoot);
    }

    [Fact]
    public void uses_a_saved_folder_and_keeps_pc_files_local()
    {
        using var temp = new TempFolder();
        var synced = temp.Sub("OneDrive-InvoiceDesk");

        DataLocation.Save(temp.Path, synced);
        var paths = DataLocation.Resolve(temp.Path);

        Assert.Equal(synced, paths.DataRoot);
        Assert.Equal(temp.Path, paths.LocalRoot);
        Assert.StartsWith(synced, paths.Database);
        Assert.StartsWith(temp.Path, paths.WebView);
        Assert.StartsWith(temp.Path, paths.Prefs);
        Assert.StartsWith(temp.Path, paths.Staging);
    }

    [Fact]
    public void saving_the_local_folder_clears_the_pointer()
    {
        using var temp = new TempFolder();
        DataLocation.Save(temp.Path, temp.Sub("elsewhere"));

        DataLocation.Save(temp.Path, temp.Path);

        Assert.Null(DataLocation.Read(temp.Path));
        Assert.Equal(temp.Path, DataLocation.Resolve(temp.Path).DataRoot);
    }
}

public class DataMoverTests
{
    [Fact]
    public async Task move_copies_everything_and_points_there()
    {
        await using var env = await TestEnv.CreateAsync();
        await env.AddClientAsync("Acme Pty Ltd");
        await env.Get<TransactionService>().SaveAsync(new Transaction
        {
            Direction = Direction.Out, Date = env.Today, AmountCents = 1100, GstCents = 100, Party = "Officeworks",
        }, [await env.StageFileAsync("receipt.jpg")], []);
        using var target = new TempFolder();
        var destination = target.Sub("InvoiceDesk");

        env.Get<DataMover>().MoveTo(destination);

        Assert.Equal(destination, DataLocation.Read(env.Paths.LocalRoot));
        Assert.True(File.Exists(Path.Combine(destination, "invoicedesk.db")));
        Assert.Single(Directory.GetFiles(Path.Combine(destination, "attachments"), "*", SearchOption.AllDirectories));
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(destination, "invoicedesk.db")};Pooling=False").Options;
        await using var db = new AppDbContext(options);
        Assert.Equal(1, await db.Clients.CountAsync());
    }

    [Fact]
    public async Task move_refuses_a_folder_that_already_has_data()
    {
        await using var env = await TestEnv.CreateAsync();
        using var target = new TempFolder();
        File.WriteAllText(target.Sub("invoicedesk.db"), "");

        Assert.Throws<ValidationException>(() => env.Get<DataMover>().MoveTo(target.Path));
    }

    [Fact]
    public async Task move_refuses_a_folder_inside_the_current_one()
    {
        await using var env = await TestEnv.CreateAsync();

        Assert.Throws<ValidationException>(() => env.Get<DataMover>().MoveTo(Path.Combine(env.Paths.DataRoot, "inside")));
    }

    [Fact]
    public async Task using_existing_data_needs_a_database()
    {
        await using var env = await TestEnv.CreateAsync();
        using var empty = new TempFolder();
        var mover = env.Get<DataMover>();

        Assert.Throws<ValidationException>(() => mover.UseExisting(empty.Path));

        File.WriteAllText(empty.Sub("invoicedesk.db"), "");
        mover.UseExisting(empty.Path);
        Assert.Equal(empty.Path, DataLocation.Read(env.Paths.LocalRoot));
    }

    [Fact]
    public async Task finds_the_data_folder_one_level_down()
    {
        await using var env = await TestEnv.CreateAsync();
        using var synced = new TempFolder();
        Directory.CreateDirectory(synced.Sub("InvoiceDesk"));
        File.WriteAllText(Path.Combine(synced.Sub("InvoiceDesk"), "invoicedesk.db"), "");

        Assert.Equal(synced.Sub("InvoiceDesk"), DataMover.FindDataFolder(synced.Path));
        Assert.Null(DataMover.FindDataFolder(env.Paths.Attachments));
    }
}

public class DataLockTests
{
    static DataLock LockFor(TestEnv env, string machine, int pid = 100) =>
        new(env.Paths, env.Clock, machine, "tim", pid);

    [Fact]
    public async Task no_lock_means_nobody_else()
    {
        await using var env = await TestEnv.CreateAsync();
        Assert.Null(LockFor(env, "DESK").ReadOther());
    }

    [Fact]
    public async Task a_fresh_lock_from_another_pc_is_reported()
    {
        await using var env = await TestEnv.CreateAsync();
        LockFor(env, "LAPTOP").Acquire();
        env.Clock.Advance(TimeSpan.FromMinutes(1));

        var other = LockFor(env, "DESK").ReadOther();

        Assert.NotNull(other);
        Assert.Equal("LAPTOP", other!.Machine);
    }

    [Fact]
    public async Task a_stale_lock_is_ignored()
    {
        await using var env = await TestEnv.CreateAsync();
        LockFor(env, "LAPTOP").Acquire();
        env.Clock.Advance(TimeSpan.FromMinutes(4));

        Assert.Null(LockFor(env, "DESK").ReadOther());
    }

    [Fact]
    public async Task heartbeat_keeps_a_lock_fresh()
    {
        await using var env = await TestEnv.CreateAsync();
        var laptop = LockFor(env, "LAPTOP");
        laptop.Acquire();
        env.Clock.Advance(TimeSpan.FromMinutes(2));
        laptop.Heartbeat();
        env.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.NotNull(LockFor(env, "DESK").ReadOther());
    }

    [Fact]
    public async Task a_lock_left_by_this_pc_is_ignored()
    {
        await using var env = await TestEnv.CreateAsync();
        LockFor(env, "DESK", pid: 1).Acquire();

        Assert.Null(LockFor(env, "DESK", pid: 2).ReadOther());
    }

    [Fact]
    public async Task release_only_removes_our_own_lock()
    {
        await using var env = await TestEnv.CreateAsync();
        var desk = LockFor(env, "DESK");
        desk.Acquire();
        LockFor(env, "LAPTOP").Acquire();

        desk.Release();

        Assert.True(File.Exists(env.Paths.LockFile));
        LockFor(env, "LAPTOP").Release();
        Assert.False(File.Exists(env.Paths.LockFile));
    }
}

public class SyncSafeDatabaseTests
{
    [Fact]
    public async Task database_uses_a_rollback_journal()
    {
        await using var env = await TestEnv.CreateAsync();
        await using var db = await env.Get<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA journal_mode";

        Assert.Equal("delete", (string?)await command.ExecuteScalarAsync());
    }
}
