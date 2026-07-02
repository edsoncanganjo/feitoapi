using feitoapi.api.Documents;
using feitoapi.api.Models;
using QuestPDF.Fluent;

namespace feitoapi.api.Services;

public class PdfService
{
    public byte[] GenerateInvoice(InvoiceRequest req, byte[]? logo)
    {
        var totals = InvoiceCalculator.Compute(req.Items);
        return new InvoiceDocument(req, totals, logo).GeneratePdf();
    }

    public byte[] GenerateReceipt(ReceiptRequest req, byte[]? logo)
    {
        var totals = InvoiceCalculator.Compute(req.Items);
        return new ReceiptDocument(req, totals, logo).GeneratePdf();
    }
}