// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.App.Ui;

// keyboard shortcuts arrive from one js listener and fan out to whichever page cares
public sealed class ShortcutService
{
    public event Action<string>? Pressed;

    public void Raise(string key) => Pressed?.Invoke(key);
}
