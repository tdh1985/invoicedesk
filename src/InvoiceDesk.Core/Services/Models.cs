using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Core.Services;

public sealed record ClientSummary(Client Client, int InvoiceCount, long BilledCents, long OutstandingCents);

public enum InvoiceFilter { All, Draft, Sent, Overdue, Paid, Void }

public sealed record InvoiceSummary(
    int Id, string Number, int ClientId, string ClientName, DateOnly IssueDate, DateOnly DueDate,
    long TotalCents, long GstCents, long PaidCents, long BalanceCents, DisplayStatus Status, bool IsTaxInvoice)
{
    public static InvoiceSummary From(Invoice inv, DateOnly today)
    {
        var totals = inv.Totals();
        var paid = inv.PaidCents;
        return new InvoiceSummary(
            inv.Id, inv.Number, inv.ClientId, inv.Client?.Name ?? "", inv.IssueDate, inv.DueDate,
            totals.TotalCents, totals.GstCents, paid, totals.TotalCents - paid,
            InvoiceStatusResolver.Resolve(inv.Status, totals.TotalCents, paid, inv.DueDate, today),
            totals.IsTaxInvoice);
    }

    // sent means still waiting on money, which includes part paid and overdue
    public bool Matches(InvoiceFilter filter) => filter switch
    {
        InvoiceFilter.Draft => Status == DisplayStatus.Draft,
        InvoiceFilter.Sent => Status is DisplayStatus.Sent or DisplayStatus.PartPaid or DisplayStatus.Overdue,
        InvoiceFilter.Overdue => Status == DisplayStatus.Overdue,
        InvoiceFilter.Paid => Status == DisplayStatus.Paid,
        InvoiceFilter.Void => Status == DisplayStatus.Void,
        _ => true,
    };

    public bool IsAwaitingPayment => Matches(InvoiceFilter.Sent);
}

public sealed record PaymentInput(DateOnly Date, long AmountCents, PaymentMethod Method, string Note);

public sealed record TransactionFilter(
    Direction? Direction = null, DateRange? Range = null, int? CategoryId = null, string? Search = null);

public sealed record MonthBar(DateOnly Month, long InCents, long OutCents);

public enum ActivityKind { InvoiceCreated, InvoiceSent, PaymentReceived, Income, Expense }

public sealed record ActivityItem(DateTime At, ActivityKind Kind, int EntityId, string Title, string Detail, long AmountCents);

public sealed record DashboardData(
    long OutstandingCents, int OutstandingCount, long OverdueCents, int OverdueCount,
    long ReceivedMonthCents, long SpentMonthCents, long ProfitFyCents,
    long GstCollectedQuarterCents, long GstPaidQuarterCents, string FyLabel, string QuarterLabel,
    IReadOnlyList<MonthBar> Months, IReadOnlyList<InvoiceSummary> Overdue, IReadOnlyList<ActivityItem> Recent)
{
    public long GstNetQuarterCents => GstCollectedQuarterCents - GstPaidQuarterCents;
}

public enum SearchKind { Client, Invoice, Transaction }

public sealed record SearchResult(SearchKind Kind, int Id, string Title, string Subtitle, long? AmountCents = null, DateOnly? Date = null);

internal static class Text
{
    public static string Clean(string? value) => value?.Trim() ?? "";

    public static bool Has(string? haystack, string needle) =>
        haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
