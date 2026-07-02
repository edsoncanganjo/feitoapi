using System.Text.Json.Serialization;
using feitoapi.api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Threading.RateLimiting;
using feitoapi.api.Infrastructure;
using feitoapi.api.Models;
using QuestPDF.Infrastructure;
using Scalar.AspNetCore;
using static System.Text.Json.JsonNamingPolicy;

// QuestPDF Community license (free while company revenue < $1M/year). See https://www.questpdf.com/license/
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateSlimBuilder(args);

// Cap request body size to protect against oversized payloads (logos are base64, ~1MB max).
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 4 * 1024 * 1024); // 4 MB

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(CamelCase));
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSingleton<PdfService>();
builder.Services.AddOpenApi();

// Per-client rate limiting: partition by API key (or client IP as fallback).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var partitionKey =
            ctx.Request.Headers.TryGetValue(ApiKeyMiddleware.ApiKeyHeader, out var k) && !string.IsNullOrEmpty(k)
                ? k.ToString()
                : ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimit:PermitPerMinute", 60),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

app.MapOpenApi();                              // OpenAPI doc at /openapi/v1.json
app.MapScalarApiReference(o => o.WithTitle("Invoice PDF API")); // interactive docs at /scalar/v1

app.UseMiddleware<ApiKeyMiddleware>();
app.UseRateLimiter();

// ---- Endpoints ----

app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

app.MapGet("api/v1/health", () => Results.Ok(new { status = "ok", service = "invoice-pdf-api", version = "1.0" }))
    .WithName("Health")
    .WithTags("System");

app.MapPost("api/v1/invoices", (InvoiceRequest req, PdfService pdf) =>
    {
        if (Normalize(req) is { } bad) return bad;

        if (DecodeLogo(req.LogoBase64, out var logo) is { } logoErr) return logoErr;

        var bytes = pdf.GenerateInvoice(req, logo);
        return PdfResult(bytes, FileName("invoice", req.Number));
    })
    .WithName("CreateInvoice")
    .WithTags("Documents")
    .Accepts<InvoiceRequest>("application/json")
    .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
    .ProducesProblem(StatusCodes.Status400BadRequest);

app.MapPost("api/v1/receipts", (ReceiptRequest req, PdfService pdf) =>
    {
        if (NormalizeReceipt(req) is { } bad) return bad;

        if (DecodeLogo(req.LogoBase64, out var logo) is { } logoErr) return logoErr;

        var bytes = pdf.GenerateReceipt(req, logo);
        return PdfResult(bytes, FileName("receipt", req.Number));
    })
    .WithName("CreateReceipt")
    .WithTags("Documents")
    .Accepts<ReceiptRequest>("application/json")
    .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
    .ProducesProblem(StatusCodes.Status400BadRequest);

await app.RunAsync();
return;

// ---- Helpers ----

// Validates + fills defaults for invoices. Returns an error result, or null when OK.
static IResult? Normalize(InvoiceRequest req)
{
    if (!ModelValidation.TryValidate(req, out var errors))
        return Results.ValidationProblem(ToDict(errors));

    req.Currency = req.Currency.Trim().ToUpperInvariant();
    req.IssueDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
    req.Number = string.IsNullOrWhiteSpace(req.Number) ? "—" : req.Number.Trim();

    if (req.DueDate is { } due && due < req.IssueDate)
        return Results.ValidationProblem(ToDict(new[] { "dueDate: must be on or after issueDate." }));

    return null;
}

static IResult? NormalizeReceipt(ReceiptRequest req)
{
    if (!ModelValidation.TryValidate(req, out var errors))
        return Results.ValidationProblem(ToDict(errors));

    req.Currency = req.Currency.Trim().ToUpperInvariant();
    req.IssueDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
    req.Number = string.IsNullOrWhiteSpace(req.Number) ? "—" : req.Number.Trim();
    return null;
}

static IResult? DecodeLogo(string? base64, out byte[]? logo)
{
    var result = ImageValidator.TryDecode(base64, out logo);
    return result switch
    {
        ImageValidator.Result.Ok or ImageValidator.Result.None => null,
        ImageValidator.Result.TooLarge => Results.ValidationProblem(ToDict(new[] { "logoBase64: image exceeds the 1MB limit." })),
        ImageValidator.Result.InvalidBase64 => Results.ValidationProblem(ToDict(new[] { "logoBase64: not valid base64." })),
        ImageValidator.Result.UnsupportedFormat => Results.ValidationProblem(ToDict(new[] { "logoBase64: only PNG or JPEG is supported." })),
        _ => Results.ValidationProblem(ToDict(new[] { "logoBase64: could not be processed." }))
    };
}

static IResult PdfResult(byte[] bytes, string fileName) =>
    Results.File(bytes, "application/pdf", fileDownloadName: fileName);

static string FileName(string kind, string? number)
{
    var safe = new string((number ?? "document")
        .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray())
        .Trim('-');
    if (string.IsNullOrEmpty(safe)) safe = "document";
    return $"{kind}-{safe}.pdf";
}

static Dictionary<string, string[]> ToDict(IEnumerable<string> errors)
{
    return errors
        .Select(e =>
        {
            var idx = e.IndexOf(':');
            return idx > 0 ? (Key: e[..idx].Trim(), Msg: e[(idx + 1)..].Trim()) : (Key: "request", Msg: e);
        })
        .GroupBy(x => x.Key)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Msg).ToArray());
}

// Exposed for integration tests.
public partial class Program { }
