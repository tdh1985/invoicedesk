// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core;
using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceDesk.Tests.TestSupport;

// a throwaway data root with real sqlite so services run as in the app
public sealed class TestEnv : IAsyncDisposable
{
    public static readonly DateOnly DefaultToday = new(2026, 9, 12);

    TestEnv(string root, DateOnly today)
    {
        Paths = new AppPaths(root);
        Clock = new FixedClock(default);
        Clock.SetToday(today);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddInvoiceDeskCore(Paths);
        Services = services.BuildServiceProvider();
    }

    public AppPaths Paths { get; }
    public FixedClock Clock { get; }
    public ServiceProvider Services { get; }
    public DateOnly Today => Clock.Today();

    public static async Task<TestEnv> CreateAsync(DateOnly? today = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "InvoiceDeskTests", Guid.NewGuid().ToString("N"));
        var env = new TestEnv(root, today ?? DefaultToday);
        await env.Get<DatabaseInitializer>().InitializeAsync();
        return env;
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Paths.DataRoot, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
