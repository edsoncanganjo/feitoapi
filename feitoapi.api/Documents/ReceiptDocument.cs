using feitoapi.api.Models;
using feitoapi.api.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace feitoapi.api.Documents;

/// <summary>
/// A compact single-page receipt (RECIBO) layout.
/// </summary>
public sealed class ReceiptDocument : IDocument
{
    private readonly ReceiptRequest _req;
    private readonly DocumentTotals _totals;
    private readonly Localization.Labels _l;
    private readonly string _accent;
    private readonly byte[]? _logo;

    public ReceiptDocument(ReceiptRequest req, DocumentTotals totals, byte[]? logo)
    {
        _req = req;
        _totals = totals;
        _l = Localization.For(req.Language);
        _accent = DocumentStyle.SanitizeColor(req.AccentColor) ?? "#1F4E79";
        _logo = logo;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{_l.Receipt} {_req.Number}".Trim(),
        Author = _req.Seller.Name,
        Producer = "InvoicePdfApi"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(28);
            page.DefaultTextStyle(DocumentStyle.BaseText);

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    if (_logo is not null)
                        col.Item().Height(40).AlignLeft().Image(_logo).FitHeight();
                    else
                        col.Item().Text(_req.Seller.Name).FontSize(13).Bold().FontColor(_accent);
                });
                row.ConstantItem(160).Column(col =>
                {
                    col.Item().AlignRight().Text(_l.Receipt).FontSize(18).Bold().FontColor(_accent);
                    col.Item().AlignRight().PaddingTop(4).Text($"{_l.Number}: {_req.Number}").SemiBold();
                    col.Item().AlignRight().Text($"{_l.IssueDate}: {_req.IssueDate:yyyy-MM-dd}");
                });
            });

            page.Content().PaddingVertical(10).Column(col =>
            {
                col.Item().Element(c => DocumentStyle.ComposeParty(c, _req.Seller, _l, includeName: _logo is not null));

                if (_req.Client is not null)
                    col.Item().PaddingTop(6).Background("#F5F7FA").Padding(6).Column(c =>
                    {
                        c.Item().Text(_l.BillTo).FontSize(8).Bold().FontColor(_accent);
                        c.Item().Element(x => DocumentStyle.ComposeParty(x, _req.Client, _l, includeName: true));
                    });

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(4);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(2);
                    });
                    table.Header(header =>
                    {
                        DocumentStyle.HeaderCell(header.Cell(), _accent, _l.Description, right: false);
                        DocumentStyle.HeaderCell(header.Cell(), _accent, _l.Qty, right: true);
                        DocumentStyle.HeaderCell(header.Cell(), _accent, _l.LineTotal, right: true);
                    });
                    foreach (var line in _totals.Lines)
                    {
                        DocumentStyle.BodyCell(table.Cell(), line.Item.Description, right: false);
                        DocumentStyle.BodyCell(table.Cell(), line.Item.Quantity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), right: true);
                        DocumentStyle.BodyCell(table.Cell(), Localization.Money(line.GrossAmount, _req.Currency, _req.Language), right: true);
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Column(c =>
                {
                    DocumentStyle.TotalRow(c, _l.Subtotal, Localization.Money(_totals.Subtotal, _req.Currency, _req.Language), bold: false);
                    DocumentStyle.TotalRow(c, _l.Tax, Localization.Money(_totals.TotalTax, _req.Currency, _req.Language), bold: false);
                    c.Item().PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text(_l.Total).Bold().FontColor(_accent);
                        row.ConstantItem(120).AlignRight().Text(Localization.Money(_totals.Total, _req.Currency, _req.Language)).Bold().FontColor(_accent);
                    });
                });

                if (!string.IsNullOrWhiteSpace(_req.PaymentMethod))
                    col.Item().PaddingTop(10).Text($"{_l.PaymentMethod}: {_req.PaymentMethod}").SemiBold();

                if (!string.IsNullOrWhiteSpace(_req.Notes))
                    col.Item().PaddingTop(6).Text(_req.Notes!);
            });

            page.Footer().Text(_l.GeneratedBy).FontSize(7).FontColor("#999999");
        });
    }
}
