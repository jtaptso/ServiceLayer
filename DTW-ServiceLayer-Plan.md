# DTW via SAP B1 Service Layer — Project Plan

## Overview

A modern replacement for SAP Business One's Data Transfer Workbench (DTW), built on the Service Layer REST API instead of the legacy DI API/COM. Accepts CSV, Excel (.xlsx), and TXT files and imports data into SAP B1 via HTTP.

**Stack:** C# / .NET 10 | ASP.NET Core Web API | Blazor WebAssembly | Clean Architecture

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────┐
│                  Web UI (Blazor WASM)                │
│  Upload: Header file + Addresses file + Contacts file│
│  Import mode, progress tracking, result report       │
└──────────────────────┬───────────────────────────────┘
                       │ HTTP multipart/form-data
┌──────────────────────▼───────────────────────────────┐
│               ASP.NET Core Web API                   │
│                                                      │
│  File Parser (CSV/TXT/XLSX)                          │
│   ├─ BusinessPartners.csv  → List<ParsedRowDto>      │
│   ├─ BPAddresses.csv       → List<ParsedRowDto>      │
│   └─ ContactEmployees.csv  → List<ParsedRowDto>      │
│                    │                                 │
│             BPAssembler                              │
│   (groups rows by CardCode → BusinessPartner entity) │
│   (UDF columns U_* passed through automatically)     │
│                    │                                 │
│             Validator (FluentValidation)              │
│                    │                                 │
│             SL Client (HttpClient + cookie session)  │
└──────────────────────┬───────────────────────────────┘
                       │ HTTPS / REST
┌──────────────────────▼───────────────────────────────┐
│            SAP B1 Service Layer                      │
│  POST  /b1s/v1/BusinessPartners                      │
│  PATCH /b1s/v1/BusinessPartners('{CardCode}')        │
└──────────────────────────────────────────────────────┘
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
  IBPAssembler.cs           ← Port for multi-file assembly
DTOs/
  ParsedRowDto.cs           ← One row from any file
  BPImportFilesDto.cs       ← Groups the 3 parsed file results
  ImportJobDto.cs
Assembly/
  BPAssembler.cs            ← Groups rows by CardCode → BusinessPartner
Validation/
  BusinessPartnerValidator.cs   ← FluentValidation rules
```

### Layer 3 — `Infrastructure` (implements Application ports)

```
ServiceLayer/
  ServiceLayerClient.cs     ← HttpClient + cookie session management
  ServiceLayerConfig.cs     ← Base URL, company DB, credentials
  DTOs/
    SLBusinessPartner.cs    ← SAP B1 SL JSON model (+ [JsonExtensionData] for UDFs)
    SLBPAddress.cs          ← (+ [JsonExtensionData] for UDFs)
    SLContactEmployee.cs    ← (+ [JsonExtensionData] for UDFs)
Parsing/
  CsvParser.cs              ← CsvHelper
  ExcelParser.cs            ← EPPlus
  TxtParser.cs              ← Delimiter-based TXT
  FileParserResolver.cs     ← Selects parser by file extension
Mapping/
  BusinessPartnerMapper.cs  ← ParsedRow → SL DTO (known fields + U_* UDFs)
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
  ImportController.cs       ← POST /api/import/business-partners (3 optional files)
  JobsController.cs         ← GET /api/jobs/{id}
Program.cs                  ← DI composition root
appsettings.json            ← SL connection settings
```

### Layer 5 — `Web` (Blazor WebAssembly)

```
Pages/
  Import.razor              ← 3-file upload (header + addresses + contacts) + mode selection
  Results.razor             ← Job progress + per-row results
Services/
  ImportApiClient.cs        ← Typed HttpClient → Api
Shared/
  MultiFileUploadComponent.razor  ← Separate drop zones per template type
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

| Project          | Packages                                                          |
|------------------|-------------------------------------------------------------------|
| `Application`    | `FluentValidation`, `MediatR`                                     |
| `Infrastructure` | `CsvHelper`, `EPPlus`, `Microsoft.Extensions.Http`               |
| `Api`            | `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`              |
| `Web`            | `Microsoft.AspNetCore.Components.WebAssembly`                     |
| `Shared`         | _(none — pure C# records/DTOs)_                                  |

---

## Phase 1 Scope — Business Partners

### Import Strategy — DTW-style Multi-File (Option B)

Each import job accepts **up to 3 separate files**, each matching a specific template. Files are linked by `CardCode`. Only the header file is required.

| File (any name) | Template       | Linked by | Required |
|-----------------|----------------|-----------|----------|
| e.g. `BusinessPartners.csv` | `BPHeader`  | _(primary)_ | Yes |
| e.g. `BPAddresses.csv`      | `BPAddress` | `CardCode`  | No  |
| e.g. `ContactEmployees.csv` | `BPContact` | `CardCode`  | No  |

After parsing, the **BPAssembler** groups all rows by `CardCode` and builds complete `BusinessPartner` objects (with child collections) before import.

### Supported Input Formats (per file)
- `.csv` — comma or semicolon delimited
- `.txt` — tab or delimiter separated (DTW-style)
- `.xlsx` — Excel workbook (first sheet)

### User-Defined Fields (UDFs)

UDF columns are supported on **all three files**. Any column prefixed with `U_` is automatically passed through to the Service Layer payload via `[JsonExtensionData]` on the SL DTOs — no code change required per UDF.

Example header file with UDFs:
```
CardCode, CardName, CardType, Phone1, U_TaxRegion, U_CustomerTier
C001, Acme Corp, C, 555-0100, Northeast, Gold
```

Example address file with UDFs:
```
CardCode, AddressName, AddressType, Street, City, U_Region
C001, Bill To, bo_BillTo, 123 Main St, New York, NY
```

### Business Partner Fields

**Header file (`BPHeader`):**

| Column           | SL Property       | Required |
|------------------|-------------------|----------|
| `CardCode`       | `CardCode`        | Yes      |
| `CardName`       | `CardName`        | Yes      |
| `CardType`       | `CardType`        | Yes (`C` / `S` / `L`) |
| `GroupCode`      | `GroupCode`       | No       |
| `Currency`       | `Currency`        | No       |
| `PayTermsGrpCode`| `PayTermsGrpCode` | No       |
| `Phone1`         | `Phone1`          | No       |
| `EmailAddress`   | `EmailAddress`    | No       |
| `Website`        | `Website`         | No       |
| `FederalTaxID`   | `FederalTaxID`    | No       |
| `U_*`            | _(UDF)_           | No — passed through automatically |

**Address file (`BPAddress`):**

| Column        | SL Property   | Notes                        |
|---------------|---------------|------------------------------|
| `CardCode`    | _(link key)_  | Must match a header row      |
| `AddressName` | `AddressName` |                              |
| `AddressType` | `AddressType` | `bo_BillTo` or `bo_ShipTo`  |
| `Street`      | `Street`      |                              |
| `City`        | `City`        |                              |
| `ZipCode`     | `ZipCode`     |                              |
| `Country`     | `Country`     | Two-letter ISO code          |
| `State`       | `State`       |                              |
| `U_*`         | _(UDF)_       | Passed through automatically |

**Contact file (`BPContact`):**

| Column        | SL Property   | Notes                        |
|---------------|---------------|------------------------------|
| `CardCode`    | _(link key)_  | Must match a header row      |
| `Name`        | `Name`        |                              |
| `FirstName`   | `FirstName`   |                              |
| `LastName`    | `LastName`    |                              |
| `Phone1`      | `Phone1`      |                              |
| `MobilePhone` | `MobilePhone` |                              |
| `E_Mail`      | `E_Mail`      |                              |
| `Position`    | `Position`    |                              |
| `U_*`         | _(UDF)_       | Passed through automatically |

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

| Phase | Goal                                                                        | Status  |
|-------|-----------------------------------------------------------------------------|---------|
| 1     | Scaffold solution, projects, references, folder structure                   | Planned |
| 2     | Domain entities, enums, value objects                                       | Planned |
| 3     | Service Layer auth client (login, session cookie, keep-alive)               | Planned |
| 4     | CSV / TXT / XLSX file parser                                                | Planned |
| 5     | BPAssembler — group parsed rows by CardCode into `BusinessPartner` objects  | Planned |
| 6     | BP Mapper (known fields + UDF passthrough) + FluentValidation rules         | Planned |
| 7     | `ImportBusinessPartners` use case (MediatR command/handler)                 | Planned |
| 8     | API controllers + Scalar docs                                               | Planned |
| 9     | Blazor WASM: multi-file upload page + results page                          | Planned |
| 10    | Upsert mode (GET CardCode → PATCH if exists)                                | Planned |
| 11    | Unit & integration tests                                                    | Planned |
| 12    | Additional object types (Items, Sales Orders, etc.)                         | Planned |
| 13    | Option A/C import modes selectable by user                                  | Planned |

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
