# DTW via SAP B1 Service Layer — Project Plan

## Overview

A modern replacement for SAP Business One's Data Transfer Workbench (DTW), built on the Service Layer REST API instead of the legacy DI API/COM. Accepts CSV, Excel (.xlsx), and TXT files and imports data into SAP B1 via HTTP.

**Stack:** C# / .NET 8 | ASP.NET Core Web API | Blazor WebAssembly | Clean Architecture

---

## Architecture Overview

```
┌─────────────────────────────────────┐
│        Web UI (Blazor WASM)         │
│  File upload, column mapping,        │
│  progress tracking, result report   │
└────────────────┬────────────────────┘
                 │ HTTP (REST)
┌────────────────▼────────────────────┐
│        ASP.NET Core Web API         │
│  ┌────────────┐  ┌───────────────┐  │
│  │File Parser │  │  Validator    │  │
│  │CSV/XLS/TXT │  │ FluentValid.  │  │
│  └─────┬──────┘  └──────┬────────┘  │
│        └────────┬────────┘           │
│         ┌───────▼───────┐            │
│         │  BP Mapper    │            │
│         │(cols → SL DTO)│            │
│         └───────┬───────┘            │
│         ┌───────▼───────┐            │
│         │  SL Client    │            │
│         │ (HttpClient + │            │
│         │  cookie auth) │            │
│         └───────┬───────┘            │
└─────────────────┼────────────────────┘
                  │ HTTPS / REST
┌─────────────────▼────────────────────┐
│        SAP B1 Service Layer          │
│  POST  /b1s/v1/BusinessPartners      │
│  PATCH /b1s/v1/BusinessPartners(...) │
└──────────────────────────────────────┘
```

---

## Clean Architecture — Layer Rules

Inner layers never reference outer layers.

```
Domain ← Application ← Infrastructure
                    ← Api
                    ← Web (Blazor WASM) — calls Api via HTTP
```

---

## Solution Structure

```
ServiceLayer.DTW.sln
├── src/
│   ├── ServiceLayer.DTW.Domain/           [Layer 1 — Core Entities]
│   ├── ServiceLayer.DTW.Application/      [Layer 2 — Use Cases]
│   ├── ServiceLayer.DTW.Infrastructure/   [Layer 3 — External Concerns]
│   ├── ServiceLayer.DTW.Api/              [Layer 4 — API Host]
│   ├── ServiceLayer.DTW.Web/              [Layer 5 — Blazor WASM UI]
│   └── ServiceLayer.DTW.Shared/           [Contracts — DTOs shared by Api + Web]
└── tests/
    ├── ServiceLayer.DTW.Domain.Tests/
    ├── ServiceLayer.DTW.Application.Tests/
    └── ServiceLayer.DTW.Infrastructure.Tests/
```

---

## Layer Breakdown

### Layer 1 — `Domain` (no external dependencies)

```
Models/
  BusinessPartner.cs        ← Core entity
  BPAddress.cs              ← Address sub-entity
  ContactPerson.cs          ← Contact sub-entity
Enums/
  CardType.cs               ← Customer / Supplier / Lead
  ImportMode.cs             ← AddOnly / UpdateOnly / Upsert
  ImportRowStatus.cs        ← Success / Error / Skipped
ValueObjects/
  CardCode.cs               ← Validated card code
Interfaces/
  IImportRepository.cs      ← Port (implemented in Infrastructure)
```

### Layer 2 — `Application` (depends on Domain only)

```
UseCases/
  ImportBusinessPartners/
    ImportBusinessPartnersCommand.cs
    ImportBusinessPartnersHandler.cs    ← MediatR handler
    ImportBusinessPartnersResult.cs
Interfaces/
  IServiceLayerClient.cs    ← Port for SL HTTP calls
  IFileParser.cs            ← Port for file parsing
  IImportJobRepository.cs   ← Port for job persistence
DTOs/
  ParsedRowDto.cs
  ImportJobDto.cs
Validation/
  BusinessPartnerValidator.cs   ← FluentValidation rules
```

### Layer 3 — `Infrastructure` (implements Application ports)

```
ServiceLayer/
  ServiceLayerClient.cs     ← HttpClient + cookie session management
  ServiceLayerConfig.cs     ← Base URL, company DB, credentials
  DTOs/
    SLBusinessPartner.cs    ← SAP B1 SL JSON model
    SLBPAddress.cs
    SLContactEmployee.cs
Parsing/
  CsvParser.cs              ← CsvHelper
  ExcelParser.cs            ← EPPlus
  TxtParser.cs              ← Delimiter-based TXT
Mapping/
  BusinessPartnerMapper.cs  ← ParsedRow → SL DTO
```

### `Shared` (referenced by both Api and Web)

```
Requests/
  StartImportRequest.cs     ← File + import options from UI
Responses/
  ImportJobResponse.cs
  ImportRowResultResponse.cs
```

### Layer 4 — `Api` (ASP.NET Core Web API)

```
Controllers/
  ImportController.cs       ← POST /api/import/business-partners
  JobsController.cs         ← GET /api/jobs/{id}
Program.cs                  ← DI composition root
appsettings.json            ← SL connection settings
```

### Layer 5 — `Web` (Blazor WebAssembly)

```
Pages/
  Import.razor              ← File upload + mode selection
  Results.razor             ← Job progress + per-row results
Services/
  ImportApiClient.cs        ← Typed HttpClient → Api
Shared/
  FileUploadComponent.razor
  ResultsTable.razor
```

---

## Project References

| Project          | References                                    |
|------------------|-----------------------------------------------|
| `Domain`         | _(none)_                                      |
| `Application`    | `Domain`                                      |
| `Infrastructure` | `Application`                                 |
| `Api`            | `Application`, `Infrastructure`, `Shared`     |
| `Web`            | `Shared`                                      |
| `*.Tests`        | project under test + xUnit + Moq              |

---

## Key NuGet Packages

| Project          | Packages                                              |
|------------------|-------------------------------------------------------|
| `Application`    | `FluentValidation`, `MediatR`                         |
| `Infrastructure` | `CsvHelper`, `EPPlus`, `Microsoft.Extensions.Http`   |
| `Api`            | `Swashbuckle.AspNetCore` (Swagger)                   |
| `Web`            | `Microsoft.AspNetCore.Components.WebAssembly`         |
| `Shared`         | _(none — pure C# records/DTOs)_                      |

---

## Phase 1 Scope — Business Partners

### Supported Input Formats
- `.csv` — comma or semicolon delimited
- `.txt` — tab or delimiter separated (DTW-style)
- `.xlsx` — Excel workbook (first sheet)

### Business Partner Fields

**Header:**

| Field            | SL Property       | Required |
|------------------|-------------------|----------|
| Card Code        | `CardCode`        | Yes      |
| Card Name        | `CardName`        | Yes      |
| Card Type        | `CardType`        | Yes      |
| Group Code       | `GroupCode`       | No       |
| Currency         | `Currency`        | No       |
| Payment Terms    | `PayTermsGrpCode` | No       |
| Phone 1          | `Phone1`          | No       |
| Email            | `EmailAddress`    | No       |
| Website          | `Website`         | No       |
| Federal Tax ID   | `FederalTaxID`    | No       |

**Addresses (`BPAddresses`):**
- `AddressName`, `AddressType` (bo_BillTo / bo_ShipTo), `Street`, `City`, `ZipCode`, `Country`, `State`

**Contacts (`ContactEmployees`):**
- `Name`, `FirstName`, `LastName`, `Phone1`, `MobilePhone`, `E_Mail`, `Position`

### Import Modes
| Mode         | Behavior                                           |
|--------------|----------------------------------------------------|
| `AddOnly`    | POST only; skip if CardCode already exists         |
| `UpdateOnly` | PATCH only; skip if CardCode does not exist        |
| `Upsert`     | GET CardCode → POST if not found, PATCH if found   |

### Error Handling Options
- **Stop on first error** — abort job at first failure
- **Continue on error** — process all rows, collect errors

### Result Log
- Per-row status: Success / Error / Skipped
- Error message from Service Layer
- Downloadable as CSV

---

## Phase Roadmap

| Phase | Goal                                                         | Status  |
|-------|--------------------------------------------------------------|---------|
| 1     | Scaffold solution, projects, references, folder structure    | Planned |
| 2     | Domain entities, enums, value objects                        | Planned |
| 3     | Service Layer auth client (login, session cookie, keep-alive)| Planned |
| 4     | CSV / TXT / XLSX file parser                                 | Planned |
| 5     | BP Mapper + FluentValidation rules                           | Planned |
| 6     | `ImportBusinessPartners` use case (MediatR command/handler)  | Planned |
| 7     | API controllers + Swagger                                    | Planned |
| 8     | Blazor WASM: upload page + results page                      | Planned |
| 9     | Upsert mode (GET CardCode → PATCH if exists)                 | Planned |
| 10    | Address + Contact sub-objects                                | Planned |
| 11    | Unit & integration tests                                     | Planned |
| 12    | Additional object types (Items, Sales Orders, etc.)          | Planned |

---

## SAP B1 Service Layer — Key Endpoints

| Action               | Method | Endpoint                              |
|----------------------|--------|---------------------------------------|
| Login                | POST   | `/b1s/v1/Login`                       |
| Logout               | POST   | `/b1s/v1/Logout`                      |
| Create BP            | POST   | `/b1s/v1/BusinessPartners`            |
| Update BP            | PATCH  | `/b1s/v1/BusinessPartners('{CardCode}')` |
| Get BP               | GET    | `/b1s/v1/BusinessPartners('{CardCode}')` |
| Check BP exists      | GET    | `/b1s/v1/BusinessPartners('{CardCode}')?$select=CardCode` |

---

## Notes

- Service Layer uses **cookie-based sessions** (`B1SESSION` cookie). The client must maintain the session and renew it before expiry.
- All requests must include `Content-Type: application/json` and `B1SESSION` cookie.
- Service Layer returns HTTP `200` for GET/PATCH and `201` for POST on success.
- Error responses include an `error.message.value` field with details.
