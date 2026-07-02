using feitoapi.api.Models;
using feitoapi.api.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace feitoapi.api.Documents;

/// <summary>
/// QuestPDF layout for a PT/EU-style invoice (FATURA).
/// </summary>
public sealed class InvoiceDocument : IDocument
{
    private readonly InvoiceRequest _req;
    private readonly DocumentTotals _totals;
    private readonly Localization.Labels _l;
    private readonly string _accent;
    private readonly byte[]? _logo;

    public InvoiceDocument(InvoiceRequest req, DocumentTotals totals, byte[]? logo)
    {
        _req = req;
        _totals = totals;
        _l = Localization.For(req.Language);
        _accent = DocumentStyle.SanitizeColor(req.AccentColor) ?? "#1F4E79";
        _logo = logo;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{_l.Invoice} {_req.Number}".Trim(),
        Author = _req.Seller.Name,
        Producer = "InvoicePdfApi"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(DocumentStyle.BaseText);

            page.Header().Element(ComposeHeader);
            page.Content().PaddingVertical(12).Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                if (_logo is not null)
                    col.Item().Height(48).AlignLeft().Image(_logo).FitHeight();
                else
                    col.Item().Text(_req.Seller.Name).FontSize(15).Bold().FontColor(_accent);

                col.Item().PaddingTop(4).Element(c => DocumentStyle.ComposeParty(c, _req.Seller, _l, includeName: _logo is not null));
            });

            row.ConstantItem(200).Column(col =>
            {
                col.Item().AlignRight().Text(_l.Invoice).FontSize(20).Bold().FontColor(_accent);
                col.Item().AlignRight().PaddingTop(6).Text($"{_l.Number}: {_req.Number}").SemiBold();
                col.Item().AlignRight().Text($"{_l.IssueDate}: {_req.IssueDate:yyyy-MM-dd}");
                if (_req.DueDate is { } due)
                    col.Item().AlignRight().Text($"{_l.DueDate}: {due:yyyy-MM-dd}");
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(10).Background("#F5F7FA").Padding(8).Column(c =>
            {
                c.Item().Text(_l.BillTo).FontSize(8).Bold().FontColor(_accent);
                c.Item().PaddingTop(2).Element(x => DocumentStyle.ComposeParty(x, _req.Client, _l, includeName: true));
            });

            col.Item().Element(ComposeItemsTable);

            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Element(ComposeTaxSummary);
                row.ConstantItem(24); // gap so totals don't crowd the IVA summary
                row.ConstantItem(210).Element(ComposeTotals);
            });

            if (!string.IsNullOrWhiteSpace(_req.PaymentReference))
                col.Item().PaddingTop(12).Text($"{_l.PaymentReference}: {_req.PaymentReference}").SemiBold();

            if (!string.IsNullOrWhiteSpace(_req.Notes))
                col.Item().PaddingTop(8).Column(c =>
                {
                    c.Item().Text(_l.Notes).FontSize(8).Bold().FontColor(_accent);
                    c.Item().Text(_req.Notes!);
                });
        });
    }

    private void ComposeItemsTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(4);    // description
                cols.RelativeColumn(1);    // qty
                cols.RelativeColumn(1.6f); // unit price
                cols.RelativeColumn(1);    // tax
                cols.RelativeColumn(1);    // discount
                cols.RelativeColumn(1.8f); // line total
            });

            table.Header(header =>
            {
                DocumentStyle.HeaderCell(header.Cell(), _accent, _l.Description, right: false);
                DocumentStyle.HeaderCell(header.Cell(), _accent, _l.Qty, right: true);
                DocumentStyle.HeaderCell(header.Cell(), _accent, _l.UnitPrice, right: true);
                DocumentStyle.HeaderCell(header.Cell(), _accent, _l.TaxRate, right: true);
                DocumentStyle.HeaderCell(header.Cell(), _accent, _l.Discount, right: true);
                DocumentStyle.HeaderCell(header.Cell(), _accent, _l.LineTotal, right: true);
            });

            foreach (var line in _totals.Lines)
            {
                var item = line.Item;
                DocumentStyle.BodyCell(table.Cell(), item.Description, right: false);
                DocumentStyle.BodyCell(table.Cell(), Quantity(item.Quantity), right: true);
                DocumentStyle.BodyCell(table.Cell(), Localization.Money(item.UnitPrice, _req.Currency, _req.Language), right: true);
                DocumentStyle.BodyCell(table.Cell(), Localization.Percent(item.TaxRate, _req.Language), right: true);
                DocumentStyle.BodyCell(table.Cell(), item.DiscountRate > 0 ? Localization.Percent(item.DiscountRate, _req.Language) : "—", right: true);
                DocumentStyle.BodyCell(table.Cell(), Localization.Money(line.NetAmount, _req.Currency, _req.Language), right: true);
            }
        });
    }

    private void ComposeTaxSummary(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text(_l.TaxSummary).FontSize(8).Bold().FontColor(_accent);
            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                });
                table.Header(header =>
                {
                    DocumentStyle.HeaderCell(header.Cell(), _accent, _l.TaxRate, right: false);
                    DocumentStyle.HeaderCell(header.Cell(), _accent, _l.TaxableBase, right: true);
                    DocumentStyle.HeaderCell(header.Cell(), _accent, _l.Tax, right: true);
                });
                foreach (var row in _totals.TaxSummary)
                {
                    DocumentStyle.BodyCell(table.Cell(), Localization.Percent(row.Rate, _req.Language), right: false);
                    DocumentStyle.BodyCell(table.Cell(), Localization.Money(row.TaxableBase, _req.Currency, _req.Language), right: true);
                    DocumentStyle.BodyCell(table.Cell(), Localization.Money(row.TaxAmount, _req.Currency, _req.Language), right: true);
                }
            });
        });
    }

    private void ComposeTotals(IContainer container)
    {
        container.Column(col =>
        {
            DocumentStyle.TotalRow(col, _l.Subtotal, Localization.Money(_totals.Subtotal, _req.Currency, _req.Language), bold: false);
            DocumentStyle.TotalRow(col, _l.Tax, Localization.Money(_totals.TotalTax, _req.Currency, _req.Language), bold: false);
            col.Item().PaddingTop(4).BorderTop(1).BorderColor(_accent).PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(_l.Total).Bold().FontColor(_accent);
                row.RelativeItem().AlignRight().Text(Localization.Money(_totals.Total, _req.Currency, _req.Language))
                    .Bold().FontColor(_accent);
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(_l.GeneratedBy).FontSize(7).FontColor("#999999");
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(t => t.FontSize(7).FontColor("#999999"));
                text.Span($"{_l.Page} ");
                text.CurrentPageNumber();
                text.Span($" {_l.Of} ");
                text.TotalPages();
            });
        });
    }

    /// <summary>Formats a quantity with up to 3 decimals, trailing zeros trimmed.</summary>
    private string Quantity(decimal qty)
    {
        var raw = Math.Round(qty, 3, MidpointRounding.AwayFromZero)
            .ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        return _req.Language == DocumentLanguage.En ? raw : raw.Replace(".", ",");
    }
}
