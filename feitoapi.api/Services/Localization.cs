using feitoapi.api.Models;

namespace feitoapi.api.Services;

/// <summary>
/// Minimal PT/EN label set and currency formatting for documents.
/// InvariantGlobalization is on, so we format money manually to keep output
/// deterministic and independent of the host machine's culture.
/// </summary>
public static class Localization
{
    public sealed record Labels(
        string Invoice,
        string Receipt,
        string Number,
        string IssueDate,
        string DueDate,
        string From,
        string BillTo,
        string TaxId,
        string Description,
        string Qty,
        string UnitPrice,
        string TaxRate,
        string Discount,
        string LineTotal,
        string Subtotal,
        string Tax,
        string Total,
        string TaxSummary,
        string TaxableBase,
        string Notes,
        string PaymentReference,
        string PaymentMethod,
        string Page,
        string Of,
        string GeneratedBy);

    private static readonly Labels Pt = new(
        Invoice: "FATURA",
        Receipt: "RECIBO",
        Number: "Nº",
        IssueDate: "Data de emissão",
        DueDate: "Data de vencimento",
        From: "Emitente",
        BillTo: "Cliente",
        TaxId: "NIF/Contribuinte",
        Description: "Descrição",
        Qty: "Qtd.",
        UnitPrice: "Preço unit.",
        TaxRate: "IVA",
        Discount: "Desc.",
        LineTotal: "Total",
        Subtotal: "Subtotal",
        Tax: "IVA",
        Total: "Total a pagar",
        TaxSummary: "Resumo de IVA",
        TaxableBase: "Base tributável",
        Notes: "Observações",
        PaymentReference: "Referência de pagamento",
        PaymentMethod: "Forma de pagamento",
        Page: "Página",
        Of: "de",
        GeneratedBy: "Documento gerado automaticamente");

    private static readonly Labels En = new(
        Invoice: "INVOICE",
        Receipt: "RECEIPT",
        Number: "No.",
        IssueDate: "Issue date",
        DueDate: "Due date",
        From: "From",
        BillTo: "Bill to",
        TaxId: "Tax ID / VAT",
        Description: "Description",
        Qty: "Qty",
        UnitPrice: "Unit price",
        TaxRate: "VAT",
        Discount: "Disc.",
        LineTotal: "Total",
        Subtotal: "Subtotal",
        Tax: "VAT",
        Total: "Total due",
        TaxSummary: "VAT summary",
        TaxableBase: "Taxable base",
        Notes: "Notes",
        PaymentReference: "Payment reference",
        PaymentMethod: "Payment method",
        Page: "Page",
        Of: "of",
        GeneratedBy: "Automatically generated document");

    public static Labels For(DocumentLanguage language) =>
        language == DocumentLanguage.En ? En : Pt;

    /// <summary>
    /// Formats an amount as "1.234,56 EUR" (pt) or "1,234.56 EUR" (en).
    /// The ISO code is appended rather than a symbol so any currency works.
    /// Formatting is done manually so it is deterministic and independent of
    /// the host culture (the app runs with InvariantGlobalization enabled).
    /// </summary>
    public static string Money(decimal amount, string currency, DocumentLanguage language)
    {
        var (thousands, dec) = Separators(language);
        var formatted = FormatGrouped(amount, 2, thousands, dec);
        return $"{formatted} {currency.ToUpperInvariant()}";
    }

    /// <summary>Formats a percentage like "23%" or "23,5%".</summary>
    public static string Percent(decimal rate, DocumentLanguage language)
    {
        var (_, dec) = Separators(language);
        // Up to 2 decimals, trailing zeros trimmed: 23.00 -> "23", 23.50 -> "23,5"
        var raw = Math.Round(rate, 2, MidpointRounding.AwayFromZero)
            .ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return $"{raw.Replace(".", dec)}%";
    }

    private static (string thousands, string dec) Separators(DocumentLanguage language) =>
        language == DocumentLanguage.En ? (",", ".") : (".", ",");

    /// <summary>Groups the integer part in thousands and applies fixed decimals, using the given separators.</summary>
    private static string FormatGrouped(decimal value, int decimals, string thousands, string dec)
    {
        var negative = value < 0;
        var abs = Math.Abs(Math.Round(value, decimals, MidpointRounding.AwayFromZero));

        // Invariant fixed-point string, e.g. "1234.56"
        var fixedStr = abs.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture);
        var parts = fixedStr.Split('.');
        var intPart = parts[0];
        var fracPart = parts.Length > 1 ? parts[1] : string.Empty;

        // Insert thousands separators into the integer part.
        var grouped = new System.Text.StringBuilder();
        for (int i = 0; i < intPart.Length; i++)
        {
            if (i > 0 && (intPart.Length - i) % 3 == 0)
                grouped.Append(thousands);
            grouped.Append(intPart[i]);
        }

        var result = decimals > 0
            ? $"{grouped}{dec}{fracPart}"
            : grouped.ToString();

        return negative ? $"-{result}" : result;
    }
}
