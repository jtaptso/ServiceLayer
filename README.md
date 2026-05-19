# ServiceLayer.DTW

A modern replacement for SAP Business One's **Data Transfer Workbench (DTW)**, built on the **Service Layer REST API** instead of the legacy DI API/COM.

Accepts CSV, TXT, and Excel (.xlsx) files and bulk-imports master data and transactions into SAP B1 via HTTP — no SAP client installation required.

---

## Features

- Upload CSV, TXT (tab/delimiter-separated), or Excel (.xlsx) files
- Import Business Partners with addresses and contact persons
- Three import modes: **Add Only**, **Update Only**, **Upsert**
- Per-row validation with detailed error messages
- Stop-on-error or continue-and-collect-errors modes
- Downloadable result log (CSV)
- Blazor WebAssembly UI + REST API

---

## Technology Stack

| Layer          | Technology                              |
|----------------|-----------------------------------------|
| Language       | C# / .NET 10                            |
| Architecture   | Clean Architecture                      |
| API            | ASP.NET Core Web API                    |
| UI             | Blazor WebAssembly                      |
| API Docs       | Scalar (`/scalar/v1`)                   |
| Validation     | FluentValidation                        |
| Mediator       | MediatR                                 |
| CSV Parsing    | CsvHelper                               |
| Excel Parsing  | EPPlus                                  |
| SAP B1         | Service Layer REST API                  |

---

## Project Structure

```
ServiceLayer.DTW.sln
├── src/
│   ├── ServiceLayer.DTW.Domain/           # Entities, enums, interfaces
│   ├── ServiceLayer.DTW.Application/      # Use cases, validators, ports
│   ├── ServiceLayer.DTW.Infrastructure/   # SL client, file parsers, mappers
│   ├── ServiceLayer.DTW.Shared/           # DTOs shared by API and UI
│   ├── ServiceLayer.DTW.Api/              # ASP.NET Core Web API host
│   └── ServiceLayer.DTW.Web/              # Blazor WebAssembly UI
└── tests/
    ├── ServiceLayer.DTW.Domain.Tests/
    ├── ServiceLayer.DTW.Application.Tests/
    └── ServiceLayer.DTW.Infrastructure.Tests/
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Access to an SAP Business One instance with Service Layer enabled
- SAP B1 Service Layer base URL (e.g., `https://your-server:50000`)

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/your-org/ServiceLayer.DTW.git
cd ServiceLayer.DTW
```

### 2. Configure the Service Layer connection

Edit `src/ServiceLayer.DTW.Api/appsettings.json`:

```json
{
  "ServiceLayer": {
    "BaseUrl":   "https://your-server:50000/b1s/v1/",
    "CompanyDb": "YourCompanyDB",
    "UserName":  "manager",
    "Password":  "yourpassword"
  }
}
```

> For production, use environment variables or .NET User Secrets instead of storing credentials in `appsettings.json`.

### 3. Run the API

```bash
dotnet run --project src/ServiceLayer.DTW.Api
```

API will be available at `https://localhost:7000`.  
Interactive API docs (Scalar): `https://localhost:7000/scalar/v1`

### 4. Run the Blazor UI

```bash
dotnet run --project src/ServiceLayer.DTW.Web
```

UI will be available at `https://localhost:7001`.  
Navigate to `/import` to upload a file.

---

## Import File Format

### Business Partner — Header

The first row must contain column headers. Column names are case-insensitive.

| Column           | Required | Notes                          |
|------------------|----------|--------------------------------|
| `CardCode`       | Yes      | Max 15 characters              |
| `CardName`       | Yes      | Max 100 characters             |
| `CardType`       | Yes      | `C` = Customer, `S` = Supplier, `L` = Lead |
| `GroupCode`      | No       | Integer                        |
| `Currency`       | No       | e.g. `USD`, `EUR`              |
| `PayTermsGrpCode`| No       | Integer                        |
| `Phone1`         | No       |                                |
| `EmailAddress`   | No       | Must be valid email format     |
| `Website`        | No       |                                |
| `FederalTaxID`   | No       |                                |

### Sample CSV

```csv
CardCode,CardName,CardType,Phone1,EmailAddress,FederalTaxID
C001,Acme Corporation,C,+1-555-0100,info@acme.com,12-3456789
S001,Big Supplier Ltd,S,+1-555-0200,orders@bigsupplier.com,98-7654321
C002,New Lead Co,L,,leads@newlead.com,
```

---

## Import Modes

| Mode           | Behaviour                                                  |
|----------------|------------------------------------------------------------|
| `AddOnly`      | Creates new records; skips rows where CardCode exists      |
| `UpdateOnly`   | Updates existing records; skips rows where CardCode is new |
| `Upsert`       | Creates if not found, updates if found                     |

---

## API Endpoints

| Method | Endpoint                                  | Description                        |
|--------|-------------------------------------------|------------------------------------|
| POST   | `/api/import/business-partners`           | Upload file and run import         |
| POST   | `/api/import/business-partners/export-log`| Download result log as CSV         |

### Import request (multipart/form-data)

| Field         | Type    | Default   | Description                      |
|---------------|---------|-----------|----------------------------------|
| `file`        | file    | —         | CSV, TXT, or XLSX file           |
| `mode`        | string  | `Upsert`  | `AddOnly`, `UpdateOnly`, `Upsert`|
| `stopOnError` | boolean | `false`   | Stop at first error              |

### Import response

```json
{
  "totalRows":    3,
  "successCount": 2,
  "errorCount":   1,
  "skippedCount": 0,
  "rowResults": [
    { "rowNumber": 1, "cardCode": "C001", "status": "Success", "message": null },
    { "rowNumber": 2, "cardCode": "S001", "status": "Success", "message": null },
    { "rowNumber": 3, "cardCode": "C002", "status": "Error",   "message": "CardName is required." }
  ]
}
```

---

## Running Tests

```bash
dotnet test ServiceLayer.DTW.sln
```

---

## Development Guide

See [DTW-ServiceLayer-DevGuide.md](DTW-ServiceLayer-DevGuide.md) for the complete step-by-step development guide covering all phases from solution scaffold to testing and deployment.

See [DTW-ServiceLayer-Plan.md](DTW-ServiceLayer-Plan.md) for the full architecture and project plan.

---

## Roadmap

- [x] Phase 1 — Solution scaffold
- [x] Phase 2 — Domain layer
- [x] Phase 3 — Service Layer auth client
- [x] Phase 4 — CSV / TXT / XLSX parsers
- [x] Phase 5 — Mapper + validator
- [x] Phase 6 — Import use case (MediatR)
- [x] Phase 7 — API controllers + Scalar docs
- [x] Phase 8 — Blazor WASM UI
- [x] Phase 9 — Upsert mode
- [x] Phase 10 — Address + contact sub-objects
- [ ] Items master data import
- [ ] Sales Orders import
- [ ] Invoices import
- [ ] Column mapping UI (drag & drop)
- [ ] Import job queue + background processing
- [ ] Authentication for the web UI

---

## License

MIT
