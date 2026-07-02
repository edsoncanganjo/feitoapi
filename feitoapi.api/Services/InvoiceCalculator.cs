using feitoapi.api.Models;

namespace feitoapi.api.Services;

/// <summary>Computed monetary values for a single line.</summary>
public sealed record LineTotals(
    LineItem Item,
    decimal NetAmount,      // qty * unitPrice, after discount, before tax
    decimal TaxAmount,      // tax on the net amount
    decimal GrossAmount);   // net + tax

/// <summary>One row of the VAT/IVA summary grouped by rate.</summary>
public sealed record TaxSummaryRow(decimal Rate, decimal TaxableBase, decimal TaxAmount);

/// <summary>Fully computed document totals.</summary>
public sealed record DocumentTotals(
    IReadOnlyList<LineTotals> Lines,
    IReadOnlyList<TaxSummaryRow> TaxSummary,
    decimal Subtotal,       // sum of net amounts
    decimal TotalTax,       // sum of tax
    decimal Total);         // subtotal + tax

/// <summary>
/// Deterministic money math for invoices/receipts.
/// All amounts are rounded to 2 decimals using banker's-safe MidpointRounding.AwayFromZero
/// (the convention used for currency in PT/EU billing).
/// </summary>
public static class InvoiceCalculator
{
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static DocumentTotals Compute(IReadOnlyList<LineItem> items)
    {
        var lines = new List<LineTotals>(items.Count);

        foreach (var item in items)
        {
            var gross = item.Quantity * item.UnitPrice;
            var discount = gross * (item.DiscountRate / 100m);
            var net = Round(gross - discount);
            var tax = Round(net * (item.TaxRate / 100m));
            lines.Add(new LineTotals(item, net, tax, Round(net + tax)));
        }

        var taxSummary = lines
            .GroupBy(l => l.Item.TaxRate)
            .OrderBy(g => g.Key)
            .Select(g => new TaxSummaryRow(
                Rate: g.Key,
                TaxableBase: Round(g.Sum(l => l.NetAmount)),
                TaxAmount: Round(g.Sum(l => l.TaxAmount))))
            .ToList();

        var subtotal = Round(lines.Sum(l => l.NetAmount));
        var totalTax = Round(lines.Sum(l => l.TaxAmount));
        var total = Round(subtotal + totalTax);

        return new DocumentTotals(lines, taxSummary, subtotal, totalTax, total);
    }
}
