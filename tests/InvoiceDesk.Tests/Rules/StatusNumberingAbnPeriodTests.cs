// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Tests.Rules;

public class InvoiceStatusResolverTests
{
    static readonly DateOnly Today = new(2026, 9, 12);

    static DisplayStatus R(InvoiceStatus s, long total, long paid, int dueOffset) =>
        InvoiceStatusResolver.Resolve(s, total, paid, Today.AddDays(dueOffset), Today);

    [Fact] public void draft_stays_draft() => Assert.Equal(DisplayStatus.Draft, R(InvoiceStatus.Draft, 100, 0, -9));
    [Fact] public void void_wins_over_payments() => Assert.Equal(DisplayStatus.Void, R(InvoiceStatus.Void, 100, 100, 5));
    [Fact] public void fully_paid_is_paid() => Assert.Equal(DisplayStatus.Paid, R(InvoiceStatus.Sent, 100, 100, -5));
    [Fact] public void overpaid_is_paid() => Assert.Equal(DisplayStatus.Paid, R(InvoiceStatus.Sent, 100, 150, 5));
    [Fact] public void past_due_with_balance_is_overdue() => Assert.Equal(DisplayStatus.Overdue, R(InvoiceStatus.Sent, 100, 40, -1));
    [Fact] public void part_paid_not_yet_due() => Assert.Equal(DisplayStatus.PartPaid, R(InvoiceStatus.Sent, 100, 40, 3));
    [Fact] public void due_today_is_not_overdue() => Assert.Equal(DisplayStatus.Sent, R(InvoiceStatus.Sent, 100, 0, 0));
}

public class InvoiceNumberingTests
{
    [Theory]
    [InlineData("INV-", 42, 4, "INV-0042")]
    [InlineData("", 7, 0, "7")]
    [InlineData("A", 12345, 4, "A12345")]
    public void formats(string prefix, int n, int pad, string expected) =>
        Assert.Equal(expected, InvoiceNumbering.Format(prefix, n, pad));
}

public class AbnTests
{
    [Theory]
    [InlineData("51 824 753 556", true)]
    [InlineData("51824753556", true)]
    [InlineData("51824753557", false)]
    [InlineData("123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("5182475355a", false)]
    public void validates_checksum(string? abn, bool ok) => Assert.Equal(ok, Abn.IsValid(abn));

    [Fact] public void formats_in_groups() => Assert.Equal("51 824 753 556", Abn.Format("51824753556"));

    [Fact] public void format_leaves_bad_input_alone() => Assert.Equal("12 3", Abn.Format("12 3"));
}

public class FinancialPeriodsTests
{
    [Fact]
    public void fy_starts_first_of_july()
    {
        Assert.Equal(new DateRange(new(2026, 7, 1), new(2027, 6, 30)), FinancialPeriods.FinancialYear(new(2026, 7, 1)));
        Assert.Equal(new DateRange(new(2025, 7, 1), new(2026, 6, 30)), FinancialPeriods.FinancialYear(new(2026, 6, 30)));
    }

    [Fact]
    public void bas_quarters_are_calendar_quarters()
    {
        Assert.Equal(new DateRange(new(2026, 7, 1), new(2026, 9, 30)), FinancialPeriods.BasQuarter(new(2026, 9, 30)));
        Assert.Equal(new DateRange(new(2026, 10, 1), new(2026, 12, 31)), FinancialPeriods.BasQuarter(new(2026, 10, 1)));
    }

    [Fact]
    public void month_handles_leap_year() =>
        Assert.Equal(new DateOnly(2028, 2, 29), FinancialPeriods.Month(new(2028, 2, 10)).End);

    [Fact]
    public void labels()
    {
        Assert.Equal("FY 2026–27", FinancialPeriods.FinancialYearLabel(new(2026, 9, 12)));
        Assert.Equal("Jul–Sep 2026", FinancialPeriods.BasQuarterLabel(new(2026, 9, 12)));
    }

    [Fact]
    public void range_contains_is_inclusive()
    {
        var r = FinancialPeriods.Month(new(2026, 9, 12));
        Assert.True(r.Contains(new(2026, 9, 1)));
        Assert.True(r.Contains(new(2026, 9, 30)));
        Assert.False(r.Contains(new(2026, 10, 1)));
    }
}
