// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Core.Rules;

public static class InvoiceStatusResolver
{
    public static DisplayStatus Resolve(InvoiceStatus status, long totalCents, long paidCents, DateOnly dueDate, DateOnly today)
    {
        if (status == InvoiceStatus.Void) return DisplayStatus.Void;
        if (status == InvoiceStatus.Draft) return DisplayStatus.Draft;
        if (totalCents - paidCents <= 0) return DisplayStatus.Paid;
        if (today > dueDate) return DisplayStatus.Overdue;
        return paidCents > 0 ? DisplayStatus.PartPaid : DisplayStatus.Sent;
    }

    public static DisplayStatus Resolve(Invoice invoice, DateOnly today) =>
        Resolve(invoice.Status, invoice.Totals().TotalCents, invoice.PaidCents, invoice.DueDate, today);
}
