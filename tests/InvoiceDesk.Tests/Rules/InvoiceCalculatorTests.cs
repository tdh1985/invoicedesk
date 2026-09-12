using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Tests.Rules;

public class InvoiceCalculatorTests
{
    static InvoiceLine L(decimal qty, long price, bool gstFree = false) =>
        new() { Quantity = qty, UnitPriceCents = price, GstFree = gstFree };

    [Fact]
    public void gst_is_ten_percent_of_taxable_lines()
    {
        var t = InvoiceCalculator.Calculate([L(1, 240000), L(1, 30000)], true, 1000);
        Assert.Equal(270000, t.SubtotalCents);
        Assert.Equal(27000, t.GstCents);
        Assert.Equal(297000, t.TotalCents);
        Assert.True(t.IsTaxInvoice);
        Assert.Equal("TAX INVOICE", t.Heading);
    }

    [Fact]
    public void gst_off_is_a_plain_invoice()
    {
        var t = InvoiceCalculator.Calculate([L(2, 5000)], false, 1000);
        Assert.Equal(10000, t.TotalCents);
        Assert.Equal(0, t.GstCents);
        Assert.False(t.IsTaxInvoice);
        Assert.Equal("INVOICE", t.Heading);
    }

    [Fact]
    public void gst_free_lines_are_excluded_from_gst()
    {
        var t = InvoiceCalculator.Calculate([L(1, 10000), L(1, 5000, true)], true, 1000);
        Assert.Equal(15000, t.SubtotalCents);
        Assert.Equal(10000, t.TaxableCents);
        Assert.Equal(1000, t.GstCents);
        Assert.Equal(16000, t.TotalCents);
    }

    [Fact]
    public void all_gst_free_lines_is_not_a_tax_invoice()
    {
        var t = InvoiceCalculator.Calculate([L(1, 5000, true)], true, 1000);
        Assert.False(t.IsTaxInvoice);
        Assert.Equal(0, t.GstCents);
    }

    [Fact]
    public void no_lines_totals_zero()
    {
        var t = InvoiceCalculator.Calculate([], true, 1000);
        Assert.Equal(0, t.TotalCents);
        Assert.False(t.IsTaxInvoice);
    }

    [Fact]
    public void line_amount_rounds_half_away_from_zero()
    {
        Assert.Equal(1235, MoneyMath.LineAmount(0.5m, 2469));
        Assert.Equal(3333, MoneyMath.LineAmount(1.5m, 2222));
    }

    [Fact]
    public void gst_is_rounded_once_on_the_taxable_total()
    {
        var t = InvoiceCalculator.Calculate([L(1, 5), L(1, 5), L(1, 5)], true, 1000);
        Assert.Equal(2, t.GstCents);
    }

    [Theory]
    [InlineData(1100, 100)]
    [InlineData(5500, 500)]
    [InlineData(1000, 91)]
    public void gst_from_inclusive_is_one_eleventh(long amount, long gst) =>
        Assert.Equal(gst, MoneyMath.GstFromInclusive(amount, 1000));

    [Fact]
    public void payment_gst_is_proportional_to_invoice_gst() =>
        Assert.Equal(13500, MoneyMath.ProportionalGst(148500, 27000, 297000));

    [Fact]
    public void totals_extension_uses_the_invoice_settings()
    {
        var inv = new Invoice { GstEnabled = true, GstRateBasisPoints = 1000, Lines = [L(3, 1000)] };
        Assert.Equal(3300, inv.Totals().TotalCents);
    }
}
