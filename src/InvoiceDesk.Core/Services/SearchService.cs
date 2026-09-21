// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed class SearchService(IDbContextFactory<AppDbContext> factory)
{
    const int PerKind = 5;

    public async Task<List<SearchResult>> SearchAsync(string? query, int limit = 12)
    {
        var term = Text.Clean(query);
        if (term.Length == 0) return [];

        await using var db = await factory.CreateDbContextAsync();
        var clients = (await db.Clients.AsNoTracking().ToListAsync())
            .Where(c => Text.Has(c.Name, term) || Text.Has(c.ContactName, term) || Text.Has(c.Email, term))
            .OrderBy(c => c.IsArchived).ThenBy(c => c.Name)
            .Take(PerKind)
            .Select(c => new SearchResult(SearchKind.Client, c.Id, c.Name, c.ContactName.Length > 0 ? c.ContactName : c.Email));

        var invoices = (await db.Invoices.AsNoTracking().AsSplitQuery().Include(i => i.Client).Include(i => i.Lines).ToListAsync())
            .Where(i => Text.Has(i.Number, term) || Text.Has(i.Client?.Name, term))
            .OrderByDescending(i => i.IssueDate)
            .Take(PerKind)
            .Select(i => new SearchResult(SearchKind.Invoice, i.Id, i.Number, i.Client?.Name ?? "", i.Totals().TotalCents, i.IssueDate));

        var txs = (await db.Transactions.AsNoTracking().ToListAsync())
            .Where(t => Text.Has(t.Party, term) || Text.Has(t.Description, term))
            .OrderByDescending(t => t.Date)
            .Take(PerKind)
            .Select(t => new SearchResult(SearchKind.Transaction, t.Id, t.Party.Length > 0 ? t.Party : t.Description,
                t.Direction == Direction.In ? "Money in" : "Money out", t.AmountCents, t.Date));

        return clients.Concat(invoices).Concat(txs).Take(limit).ToList();
    }
}
