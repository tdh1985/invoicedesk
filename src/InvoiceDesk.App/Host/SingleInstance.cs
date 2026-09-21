// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;

namespace InvoiceDesk.App.Host;

// one window per data folder, so two copies never write the same database
public sealed class SingleInstance : IDisposable
{
    readonly Mutex _mutex;
    readonly EventWaitHandle _activate;
    RegisteredWaitHandle? _registration;

    public SingleInstance(string dataRoot)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataRoot.ToLowerInvariant())))[..12];
        _mutex = new Mutex(true, $@"Local\InvoiceDesk.{key}", out var created);
        IsFirst = created;
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\InvoiceDesk.{key}.Activate");
    }

    public bool IsFirst { get; }

    public void SignalFirst() => _activate.Set();

    public void ListenForActivation(Action onActivate) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _activate.Dispose();
    }
}
