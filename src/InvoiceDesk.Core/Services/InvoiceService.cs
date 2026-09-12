using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed class InvoiceService(
    IDbContextFactory<AppDbContext> factory, AttachmentStore store, TimeProvider clock,
    ProfileService profiles, CategoryService categories)
{
    public event Action? Changed;

    public async Task<Invoice> NewDraftAsync(int? clientId = null)
    {
        var profile = await profiles.GetAsync();
        var today = clock.Today();
        return new Invoice
        {
            ClientId = clientId ?? 0,
            IssueDate = today,
            DueDate = today.AddDays(profile.PaymentTermsDays),
            Status = InvoiceStatus.Draft,
            GstEnabled = profile.GstRegistered,
            GstRateBasisPoints = profile.GstRateBasisPoints,
            Lines = [new InvoiceLine { Quantity = 1 }],
        };
    }

    public async Task<Invoice?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.AsNoTrackingWithIdentityResolution().AsSplitQuery()
            .Include(i => i.Client)
            .Include(i => i.Lines)
            .Include(i => i.Attachments)
            .Include(i => i.Payments).ThenInclude(p => p.Attachments)
            .Include(i => i.Payments).ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (inv is null) return null;

        inv.Lines = inv.Lines.OrderBy(l => l.SortOrder).ThenBy(l => l.Id).ToList();
        inv.Payments = inv.Payments.OrderBy(p => p.Date).ThenBy(p => p.Id).ToList();
        return inv;
    }

    public async Task<Invoice> SaveAsync(Invoice invoice)
    {
        var errors = new List<string>();
        if (invoice.ClientId <= 0) errors.Add("Choose a client.");
        if (invoice.DueDate < invoice.IssueDate) errors.Add("The due date can't be before the issue date.");
        ValidationException.ThrowIfAny(errors);

        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        Invoice entity;
        if (invoice.Id == 0)
        {
            var profile = await db.Profiles.SingleAsync();
            entity = new Invoice { CreatedAt = clock.Now(), Status = InvoiceStatus.Draft };
            entity.Number = await NextNumberAsync(db, profile);
            db.Invoices.Add(entity);
        }
        else
        {
            entity = await db.Invoices.Include(i => i.Lines).Include(i => i.Payments)
                         .SingleOrDefaultAsync(i => i.Id == invoice.Id)
                     ?? throw new ValidationException("This invoice no longer exists.");
            if (entity.Status == InvoiceStatus.Void) throw new ValidationException("Void invoices can't be changed.");
            if (entity.Payments.Count > 0)
                throw new ValidationException("This invoice has payments recorded, so it can't be changed.");
            db.InvoiceLines.RemoveRange(entity.Lines);
            entity.Lines = [];
        }

        entity.ClientId = invoice.ClientId;
        entity.IssueDate = invoice.IssueDate;
        entity.DueDate = invoice.DueDate;
        entity.GstEnabled = invoice.GstEnabled;
        entity.GstRateBasisPoints = invoice.GstRateBasisPoints;
        entity.Notes = Text.Clean(invoice.Notes);
        var order = 0;
        foreach (var line in invoice.Lines)
        {
            entity.Lines.Add(new InvoiceLine
            {
                SortOrder = order++,
                Description = Text.Clean(line.Description),
                Quantity = line.Quantity,
                UnitPriceCents = line.UnitPriceCents,
                GstFree = line.GstFree,
            });
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        Changed?.Invoke();
        return (await GetAsync(entity.Id))!;
    }

    public IReadOnlyList<string> ValidateForIssue(Invoice inv, BusinessProfile profile)
    {
        var errors = new List<string>();
        if (inv.ClientId <= 0) errors.Add("Choose a client.");
        if (inv.Lines.Count == 0) errors.Add("Add at least one line item.");
        for (var i = 0; i < inv.Lines.Count; i++)
        {
            var line = inv.Lines[i];
            if (string.IsNullOrWhiteSpace(line.Description)) errors.Add($"Line {i + 1} needs a description.");
            if (line.Quantity <= 0) errors.Add($"Line {i + 1} needs a quantity above zero.");
        }

        var totals = inv.Totals();
        if (totals.TotalCents < 0) errors.Add("The invoice total can't be negative.");
        if (totals.IsTaxInvoice)
        {
            // ato: a tax invoice must show the seller's identity and abn
            if (!Abn.IsValid(profile.Abn)) errors.Add("Add your ABN in Settings. Tax invoices must show it.");
            if (string.IsNullOrWhiteSpace(profile.Name)) errors.Add("Add your business name in Settings.");
        }
        return errors;
    }

    public async Task MarkSentAsync(int id, string? pdfPath)
    {
        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.Include(i => i.Lines).Include(i => i.Attachments).SingleOrDefaultAsync(i => i.Id == id)
                  ?? throw new ValidationException("This invoice no longer exists.");
        if (inv.Status == InvoiceStatus.Void) throw new ValidationException("Void invoices can't be sent.");

        var profile = await db.Profiles.AsNoTracking().SingleAsync();
        ValidationException.ThrowIfAny(ValidateForIssue(inv, profile).ToList());

        Attachment? pdf = null;
        if (pdfPath is not null)
        {
            pdf = store.Import(pdfPath, $"{inv.Number}.pdf", AttachmentKind.SentInvoicePdf);
            inv.Attachments.Add(pdf);
        }
        if (inv.Status == InvoiceStatus.Draft)
        {
            inv.Status = InvoiceStatus.Sent;
            inv.SentAt = clock.Now();
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            if (pdf is not null) store.DeleteFile(pdf);
            throw;
        }
        Changed?.Invoke();
    }

    public async Task VoidAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.FindAsync(id) ?? throw new ValidationException("This invoice no longer exists.");
        if (inv.Status == InvoiceStatus.Draft) throw new ValidationException("Drafts can be deleted instead of voided.");
        if (inv.Status == InvoiceStatus.Void) return;

        inv.Status = InvoiceStatus.Void;
        inv.VoidedAt = clock.Now();
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task DeleteDraftAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.Include(i => i.Attachments).SingleOrDefaultAsync(i => i.Id == id);
        if (inv is null) return;
        if (inv.Status != InvoiceStatus.Draft)
            throw new ValidationException("Only drafts can be deleted. Void the invoice instead.");

        var files = inv.Attachments.ToList();
        db.Invoices.Remove(inv);
        await db.SaveChangesAsync();
        store.DeleteFiles(files);
        Changed?.Invoke();
    }

    public async Task<Invoice> DuplicateAsync(int id)
    {
        var source = await GetAsync(id) ?? throw new ValidationException("This invoice no longer exists.");
        var copy = await NewDraftAsync(source.ClientId);
        copy.GstEnabled = source.GstEnabled;
        copy.GstRateBasisPoints = source.GstRateBasisPoints;
        copy.Notes = source.Notes;
        copy.Lines = source.Lines.Select(l => l.Copy()).ToList();
        return copy;
    }

    public async Task<Transaction> RecordPaymentAsync(int invoiceId, PaymentInput input, IReadOnlyList<StagedFile> receipts)
    {
        if (input.AmountCents <= 0) throw new ValidationException("Payment amount must be more than zero.");

        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.Include(i => i.Client).Include(i => i.Lines).SingleOrDefaultAsync(i => i.Id == invoiceId)
                  ?? throw new ValidationException("This invoice no longer exists.");
        if (inv.Status != InvoiceStatus.Sent)
            throw new ValidationException(inv.Status == InvoiceStatus.Draft
                ? "Mark the invoice as sent before recording a payment."
                : "Void invoices can't take payments.");

        var totals = inv.Totals();
        var sales = await categories.GetSalesAsync();
        var note = Text.Clean(input.Note);
        var payment = new Transaction
        {
            Direction = Direction.In,
            Date = input.Date,
            AmountCents = input.AmountCents,
            GstCents = totals.IsTaxInvoice ? MoneyMath.ProportionalGst(input.AmountCents, totals.GstCents, totals.TotalCents) : 0,
            CategoryId = sales.Id,
            Party = inv.Client!.Name,
            Description = note.Length == 0 ? $"Payment for {inv.Number}" : $"Payment for {inv.Number} – {note}",
            InvoiceId = inv.Id,
            Method = input.Method,
            CreatedAt = clock.Now(),
        };

        var files = store.ImportAll(receipts, AttachmentKind.Receipt);
        payment.Attachments.AddRange(files);
        db.Transactions.Add(payment);
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            store.DeleteFiles(files);
            throw;
        }

        store.DiscardStaged(receipts);
        payment.Category = sales;
        Changed?.Invoke();
        return payment;
    }

    public async Task<List<InvoiceSummary>> ListAsync(InvoiceFilter filter = InvoiceFilter.All, string? search = null, int? clientId = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        IQueryable<Invoice> query = db.Invoices.AsNoTracking().AsSplitQuery()
            .Include(i => i.Client).Include(i => i.Lines).Include(i => i.Payments);
        if (clientId is { } cid) query = query.Where(i => i.ClientId == cid);

        var today = clock.Today();
        var term = Text.Clean(search);
        return (await query.ToListAsync())
            .Select(i => InvoiceSummary.From(i, today))
            .Where(s => s.Matches(filter))
            .Where(s => term.Length == 0 || Text.Has(s.Number, term) || Text.Has(s.ClientName, term))
            .OrderByDescending(s => s.IssueDate)
            .ThenByDescending(s => s.Id)
            .ToList();
    }

    // skips numbers already used so lowering the counter in settings can't create duplicates
    static async Task<string> NextNumberAsync(AppDbContext db, BusinessProfile profile)
    {
        var used = (await db.Invoices.Select(i => i.Number).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var n = Math.Max(1, profile.NextInvoiceNumber);
        string number;
        while (used.Contains(number = InvoiceNumbering.Format(profile.InvoicePrefix, n, profile.NumberPadding))) n++;
        profile.NextInvoiceNumber = n + 1;
        return number;
    }
}
