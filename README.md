# InvoiceDesk

A Windows desktop app for sending invoices to clients and keeping track of
money in and out. Made for Australian sole traders and small businesses, so
it follows GST rules. It runs as one exe and keeps your data on your own PC.

## Features

- Invoices with GST turned on or off per invoice. The heading switches between
  **TAX INVOICE** and **INVOICE** to match, and GST-free lines are marked.
- A live A4 preview next to the editor. Exported PDFs match the preview.
- Drafts save as you type. Invoices go from draft to sent, then part-paid,
  paid or overdue. You can void a sent invoice but not delete it.
- Record payments against invoices, plus other income and expenses, with
  receipts attached (PDF, JPG, PNG or WebP, up to 25 MB each).
- Dashboard with outstanding and overdue totals, money in and out, profit for
  the financial year, GST for the current BAS quarter and a 12-month chart.
- Your business details, logo, bank details, accent colour, invoice numbering
  and payment terms.
- Clients with their own notes, website and a note printed on every invoice.
- Search everything with Ctrl+K. Ctrl+N starts a new invoice and Ctrl+S saves.
- Light and dark themes.
- Your data can live in a OneDrive, Dropbox or Google Drive folder so you can
  use it on another PC, one PC at a time.

## Requirements

- Windows 10 (1809) or later, 64-bit
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/),
  which Windows 11 already has
- [.NET 10 SDK](https://dotnet.microsoft.com/download), only if you're building it yourself

## Build and run

```powershell
dotnet run --project src/InvoiceDesk.App
```

Run the tests:

```powershell
dotnet test
```

Build the single exe (self-contained, so it doesn't need .NET installed). It
ends up in `dist\InvoiceDesk.exe`:

```powershell
dotnet publish src/InvoiceDesk.App -p:PublishProfile=SingleExe
```

## Where your data lives

Everything is in `%LOCALAPPDATA%\InvoiceDesk\` unless you move it in
**Settings → Your data**:

| Path | What it is |
|---|---|
| `invoicedesk.db` | SQLite database |
| `attachments\` | copies of receipts and logos (your originals aren't moved) |
| `exports\` | exported PDFs |
| `backups\` | a copy of the database from each start, the last 10 kept |

The app makes a backup each time it starts. You can also copy the whole folder
yourself to take your own backup.

## How the code is laid out

| Project | What's in it |
|---|---|
| `src/InvoiceDesk.Core` | Data model, GST and money maths, numbering, EF Core with SQLite, services and file storage. No UI code. |
| `src/InvoiceDesk.App` | The WPF window hosting Blazor (`BlazorWebView`), Razor pages and components, CSS and PDF export. |
| `tests/InvoiceDesk.Tests` | xUnit tests for the rules and services, run against a temp SQLite file. |

Money is stored as whole cents (`long`) and GST rates as basis points
(10% = 1000). Totals are rounded half away from zero, and GST is worked out
once per invoice rather than per line.

To change the database schema, edit the entities in `InvoiceDesk.Core/Domain`
and add a migration:

```powershell
dotnet tool restore
dotnet ef migrations add <Name> --project src/InvoiceDesk.Core
```

Migrations are applied automatically on startup.

## Disclaimer

InvoiceDesk helps you keep records. It isn't tax advice, so check your GST and
BAS figures with the ATO or your accountant.

## License

[MIT](LICENSE) © 2026 Tim Downey
