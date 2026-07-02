using System.ComponentModel.DataAnnotations;

namespace feitoapi.api.Models;

/// <summary>
/// Party (seller or client) appearing on a document.
/// </summary>
public sealed class Party
{
    /// <summary>Legal or trade name. Required.</summary>
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Tax identification number (NIF/VAT). Optional but recommended for legal invoices.</summary>
    [StringLength(30)]
    public string? TaxId { get; set; }

    [StringLength(200)]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    /// <summary>ISO 3166-1 country name or code. Defaults to Portugal.</summary>
    [StringLength(60)]
    public string? Country { get; set; }

    [StringLength(120), EmailAddress]
    public string? Email { get; set; }

    [StringLength(40)]
    public string? Phone { get; set; }
}

/// <summary>
/// A single billable line on the document.
/// </summary>
public sealed class LineItem
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    [Range(0.0001, 1_000_000)]
    public decimal Quantity { get; set; } = 1m;

    /// <summary>Unit price BEFORE tax.</summary>
    [Range(0, 100_000_000)]
    public decimal UnitPrice { get; set; }

    /// <summary>VAT/IVA rate as a percentage (e.g. 23 for 23%). Defaults to 0.</summary>
    [Range(0, 100)]
    public decimal TaxRate { get; set; }

    /// <summary>Optional per-line discount as a percentage (0-100).</summary>
    [Range(0, 100)]
    public decimal DiscountRate { get; set; }
}

public enum DocumentLanguage
{
    Pt,
    En
}

/// <summary>
/// Request payload to generate an invoice PDF.
/// </summary>
public sealed class InvoiceRequest
{
    /// <summary>Human-facing invoice number, e.g. "FT 2026/000123". If omitted a placeholder is used.</summary>
    [StringLength(60)]
    public string? Number { get; set; }

    /// <summary>Issue date (yyyy-MM-dd). Defaults to today (UTC) when omitted.</summary>
    public DateOnly? IssueDate { get; set; }

    /// <summary>Optional due date (yyyy-MM-dd).</summary>
    public DateOnly? DueDate { get; set; }

    [Required]
    public Party Seller { get; set; } = new();

    [Required]
    public Party Client { get; set; } = new();

    [Required, MinLength(1), MaxLength(200)]
    public List<LineItem> Items { get; set; } = new();

    /// <summary>ISO 4217 currency code, e.g. EUR, USD, GBP, BRL. Defaults to EUR.</summary>
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "EUR";

    /// <summary>Document language: "pt" or "en". Defaults to pt.</summary>
    public DocumentLanguage Language { get; set; } = DocumentLanguage.Pt;

    /// <summary>Optional free-text notes / payment terms shown at the bottom.</summary>
    [StringLength(1000)]
    public string? Notes { get; set; }

    /// <summary>Optional payment reference (IBAN, MB reference, etc.).</summary>
    [StringLength(200)]
    public string? PaymentReference { get; set; }

    /// <summary>Optional base64-encoded PNG/JPEG logo (raw base64, no data-URI prefix). Max ~1MB decoded.</summary>
    public string? LogoBase64 { get; set; }

    /// <summary>Optional accent colour as hex (e.g. "#0F62FE"). Defaults to a neutral blue.</summary>
    [StringLength(7)]
    public string? AccentColor { get; set; }
}

/// <summary>
/// Request payload to generate a simple receipt PDF.
/// </summary>
public sealed class ReceiptRequest
{
    [StringLength(60)]
    public string? Number { get; set; }

    public DateOnly? IssueDate { get; set; }

    [Required]
    public Party Seller { get; set; } = new();

    public Party? Client { get; set; }

    public List<LineItem> Items { get; set; } = new();

    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "EUR";

    public DocumentLanguage Language { get; set; } = DocumentLanguage.Pt;

    [StringLength(60)]
    public string? PaymentMethod { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public string? LogoBase64 { get; set; }

    [StringLength(7)]
    public string? AccentColor { get; set; }
}
