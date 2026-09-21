// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.App.Ui;

// one js key listener fans out to whichever page cares
public sealed class ShortcutService
{
    public event Action<string>? Pressed;

    public void Raise(string key) => Pressed?.Invoke(key);
}
