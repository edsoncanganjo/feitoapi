# Invoice PDF API

A small, fast, low-cost HTTP API that turns JSON into **PT/EU-compliant invoice and receipt PDFs**.
Built with **.NET 10 Minimal API + [QuestPDF](https://www.questpdf.com/)**. No database, no browser engine,
stateless — it fits on a ~€4/month VPS and is ready to list on **RapidAPI** or sell directly (B2B).

- **JSON in → PDF out.** `POST /v1/invoices`, `POST /v1/receipts`.
- **PT/EU features:** NIF/VAT fields, IVA breakdown grouped by rate, subtotal/tax/total.
- **Multi-currency** (EUR, USD, GBP, …) and **bilingual** (`pt` / `en`).
- **Branding:** optional logo (base64 PNG/JPEG) + accent colour.
- **Stateless & GDPR-friendly:** nothing is stored or logged from the payload.
- **Built-in** API-key auth (standalone or behind RapidAPI), per-client rate limiting, request validation.

---

## Quick start

### Run locally (.NET 10 SDK)

```bash
cd src/InvoiceApi
dotnet run
# Docs (Scalar UI):  http://localhost:5080/scalar/v1
# OpenAPI JSON:      http://localhost:5080/openapi/v1.json
# Health:            http://localhost:5080/v1/health
```

> With no `Auth:ApiKeys` / `Auth:RapidApiProxySecret` set, auth is **disabled** (local dev only) and a warning is logged.

### Run with Docker

```bash
docker compose up --build
# API on http://localhost:8080  (X-API-Key: demo-key-change-me)
```

### Generate a sample invoice

```bash
curl -X POST http://localhost:8080/v1/invoices \
  -H "Content-Type: application/json" \
  -H "X-API-Key: demo-key-change-me" \
  --data @samples/invoice-sample.json \
  --output invoice.pdf
```

Sample payloads live in [`samples/`](samples/).

---

## API reference

| Method & path        | Description                          | Body              | Returns          |
|----------------------|--------------------------------------|-------------------|------------------|
| `POST /v1/invoices`  | Generate an A4 invoice (FATURA)      | `InvoiceRequest`  | `application/pdf` |
| `POST /v1/receipts`  | Generate an A5 receipt (RECIBO)      | `ReceiptRequest`  | `application/pdf` |
| `GET  /v1/health`    | Liveness probe                       | –                 | `application/json` |
| `GET  /openapi/v1.json` | OpenAPI document                  | –                 | `application/json` |
| `GET  /scalar/v1`    | Interactive API docs                 | –                 | HTML             |

### `InvoiceRequest` (JSON)

| Field | Type | Notes |
|---|---|---|
| `number` | string? | e.g. `"FT 2026/000123"`. Placeholder if omitted. |
| `issueDate` | date? (`yyyy-MM-dd`) | Defaults to today (UTC). |
| `dueDate` | date? | Must be ≥ `issueDate`. |
| `seller` | Party | **required** |
| `client` | Party | **required** |
| `items` | LineItem[] | **required**, 1–200 items |
| `currency` | string | ISO 4217, default `EUR` |
| `language` | `"pt"` \| `"en"` | default `pt` |
| `notes` | string? | Free text / payment terms |
| `paymentReference` | string? | IBAN, MB reference, … |
| `logoBase64` | string? | Raw base64 PNG/JPEG, ≤ 1 MB decoded |
| `accentColor` | string? | Hex like `#0F62FE` |

**Party:** `name` (required), `taxId`, `addressLine1`, `addressLine2`, `postalCode`, `city`, `country`, `email`, `phone`.

**LineItem:** `description` (required), `quantity`, `unitPrice` (pre-tax), `taxRate` (%, e.g. `23`), `discountRate` (%, optional).

Money math is deterministic and rounded to 2 decimals (`AwayFromZero`). Tax is computed per line and summarized per rate.

### Errors

Validation failures return **HTTP 400** `application/problem+json` (RFC 9110) with per-field messages, e.g.:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Seller.Name": ["The Name field is required."],
    "Items": ["The field Items must be a string or array type with a minimum length of '1'."]
  }
}
```

Rate-limit breaches return **HTTP 429**.

---

## Authentication

Two mutually-compatible modes, configured via app settings / env vars:

| Mode | Setting | Caller sends |
|---|---|---|
| **Standalone** | `Auth:ApiKeys` = `key1,key2` | `X-API-Key: key1` |
| **Behind RapidAPI** | `Auth:RapidApiProxySecret` = `<secret>` | RapidAPI injects `X-RapidAPI-Proxy-Secret` |

Env-var form (Docker/Linux): `Auth__ApiKeys`, `Auth__RapidApiProxySecret`.
`/v1/health`, `/openapi`, `/scalar`, and `/` are always public. If **neither** setting is present, auth is disabled (dev only).

**Rate limiting:** fixed window per minute, partitioned by API key (falls back to client IP). Configure with `RateLimit:PermitPerMinute` (default 60).

---

## Deploy (cheapest → managed)

Build the image once: `docker build -t invoice-pdf-api .`

- **Hetzner / any VPS (~€4/mo):** `docker compose up -d`. Put Caddy/Traefik in front for TLS.
- **Fly.io:** `fly launch` → `fly deploy`. Set secrets: `fly secrets set Auth__ApiKeys=...`. Health check → `/v1/health`.
- **Render:** New Web Service from repo, Docker runtime, health check path `/v1/health`.
- **Azure Container Apps:** `az containerapp up` with ingress on port 8080; probe `/v1/health`.

The container listens on `:8080`, runs as non-root, and needs no volumes.

---

## Selling it

### On RapidAPI
1. Deploy publicly (HTTPS).
2. Add the API on RapidAPI → set the **base URL**, import `/openapi/v1.json`.
3. Enable **"Reject calls that don't come from RapidAPI"** and copy the proxy secret into `Auth:RapidApiProxySecret` so nobody bypasses billing.
4. Configure tiers (suggested):

| Tier | Price/mo | Quota (PDFs) |
|---|---|---|
| Free | €0 | 50 |
| Basic | €9.99 | 1,000 |
| Pro | €29.99 | 10,000 |
| Business | €99.99 | 100,000 |

### Direct B2B (higher margin)
Sell to Portuguese e-commerce / accounting / SaaS shops at **€100–500/mo** with a couple of custom templates + support. That's where the real revenue is; RapidAPI is the funnel.

> **Revenue reality:** RapidAPI alone for this niche is typically tens–low-hundreds €/month. The upside is 2–4 direct clients.

---

## Licensing & compliance notes

- **QuestPDF Community license** is free **while your company revenue is under $1M/year**. Above that you need a paid license — see <https://www.questpdf.com/license/>. The license type is set to `Community` in `Program.cs`.
- **`Microsoft.OpenApi`** is pinned to `2.7.5` to clear advisory **GHSA-v5pm-xwqc-g5wc** (untrusted-document parsing DoS; this app only *generates* its own doc, so real exposure is nil).
- **Not a certified billing system.** This produces professional invoice/receipt **documents**. It does **not** implement Portugal's SAF-T, ATCUD, or certified-software (software certificado) requirements. For legally certified invoicing in PT, integrate with certified software or add those fields deliberately. Position/sell it accordingly.
- **GDPR:** payloads contain personal data but are processed in-memory and never persisted or logged. Keep it that way.

---

## Security notes

- Logos are validated by **magic bytes** (PNG/JPEG only) and capped at **1 MB**; request body capped at 4 MB.
- No HTML/URL input and no headless browser → **no SSRF surface** (a common failure mode of HTML-to-PDF APIs).
- Proxy-secret comparison is length-constant.
- Run behind TLS and keep API keys out of source (use env vars / platform secrets).

---

## Project layout

```
InvoicePdfApi/
├─ src/InvoiceApi/
│  ├─ Program.cs                 # endpoints, auth, rate limiting, validation wiring
│  ├─ Models/InvoiceModels.cs    # request DTOs + DataAnnotations
│  ├─ Services/
│  │  ├─ InvoiceCalculator.cs    # deterministic money/tax math
│  │  ├─ Localization.cs         # PT/EN labels + manual currency formatting
│  │  ├─ ImageValidator.cs       # logo magic-byte + size validation
│  │  └─ PdfService.cs           # model → PDF bytes
│  ├─ Documents/                 # QuestPDF layouts (Invoice, Receipt, shared style)
│  └─ Infrastructure/            # API-key middleware, recursive model validation
├─ samples/                      # example JSON + generated PDFs
├─ Dockerfile / docker-compose.yml
```

## Roadmap ideas

- Credit notes / proforma document types.
- Optional QR code (e.g. ATCUD) once you add PT certification fields.
- Batch endpoint (array → zipped PDFs) for bulk clients.
- Custom template upload for white-label B2B deals.
