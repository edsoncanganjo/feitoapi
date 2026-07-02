using System.Text.RegularExpressions;
using feitoapi.api.Models;
using feitoapi.api.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace feitoapi.api.Documents;

/// <summary>
/// Shared QuestPDF cell/party rendering helpers used by invoice and receipt documents.
/// </summary>
public static partial class DocumentStyle
{
    /// <summary>
    /// Base text style. Standard/contextual ligatures are disabled because the bundled
    /// Lato font otherwise merges pairs like "ft"/"ti" into ligature glyphs that render
    /// as gaps (e.g. "software" -> "soware"). Disabling them keeps every glyph literal.
    /// </summary>
    public static TextStyle BaseText(TextStyle t) => t
        .FontSize(9)
        .FontColor("#222222")
        .DisableFontFeature("liga")  // standard ligatures
        .DisableFontFeature("clig")  // contextual ligatures
        .DisableFontFeature("dlig")  // discretionary ligatures
        .DisableFontFeature("hlig")  // historical ligatures
        .DisableFontFeature("calt"); // contextual alternates

    public static void HeaderCell(IContainer cell, string accent, string text, bool right)
    {
        var padded = cell.Background(accent).PaddingVertical(5).PaddingHorizontal(5);
        var aligned = right ? padded.AlignRight() : padded.AlignLeft();
        aligned.Text(text).FontColor("#FFFFFF").FontSize(8).Bold();
    }

    public static void BodyCell(IContainer cell, string text, bool right)
    {
        var padded = cell.BorderBottom(0.5f).BorderColor("#E0E0E0").PaddingVertical(4).PaddingHorizontal(5);
        var aligned = right ? padded.AlignRight() : padded.AlignLeft();
        aligned.Text(text).FontSize(8);
    }

    public static void TotalRow(ColumnDescriptor col, string label, string value, bool bold)
    {
        col.Item().Row(row =>
        {
            var l = row.RelativeItem().Text(label);
            var v = row.RelativeItem().AlignRight().Text(value);
            if (bold) { l.Bold(); v.Bold(); }
        });
    }

    public static void ComposeParty(IContainer container, Party p, Localization.Labels labels, bool includeName)
    {
        container.Column(col =>
        {
            if (includeName)
                col.Item().Text(p.Name).SemiBold();
            if (!string.IsNullOrWhiteSpace(p.TaxId))
                col.Item().Text($"{labels.TaxId}: {p.TaxId}");
            if (!string.IsNullOrWhiteSpace(p.AddressLine1))
                col.Item().Text(p.AddressLine1!);
            if (!string.IsNullOrWhiteSpace(p.AddressLine2))
                col.Item().Text(p.AddressLine2!);

            var cityLine = string.Join(" ", new[] { p.PostalCode, p.City }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(cityLine))
                col.Item().Text(cityLine);
            if (!string.IsNullOrWhiteSpace(p.Country))
                col.Item().Text(p.Country!);
            if (!string.IsNullOrWhiteSpace(p.Email))
                col.Item().Text(p.Email!);
            if (!string.IsNullOrWhiteSpace(p.Phone))
                col.Item().Text(p.Phone!);
        });
    }

    /// <summary>Returns a valid "#RRGGBB" color or null if the input is not a 6-digit hex.</summary>
    public static string? SanitizeColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        hex = hex.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;
        return HexColor().IsMatch(hex) ? hex : null;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
