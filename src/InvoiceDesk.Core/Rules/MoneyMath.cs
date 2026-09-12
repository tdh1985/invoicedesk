namespace InvoiceDesk.Core.Rules;

public static class MoneyMath
{
    public static long Round(decimal cents) => (long)Math.Round(cents, 0, MidpointRounding.AwayFromZero);

    public static long LineAmount(decimal quantity, long unitPriceCents) => Round(quantity * unitPriceCents);

    public static long GstOn(long taxableCents, int rateBasisPoints) => Round(taxableCents * rateBasisPoints / 10000m);

    public static long GstFromInclusive(long amountCents, int rateBasisPoints) =>
        Round(amountCents * (decimal)rateBasisPoints / (10000m + rateBasisPoints));

    // gst on a part payment, so bas figures follow cash received
    public static long ProportionalGst(long paymentCents, long invoiceGstCents, long invoiceTotalCents) =>
        invoiceTotalCents == 0 ? 0 : Round((decimal)paymentCents * invoiceGstCents / invoiceTotalCents);
}
