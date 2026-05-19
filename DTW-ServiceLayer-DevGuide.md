# DTW via SAP B1 Service Layer — Step-by-Step Development Guide

## Prerequisites

- .NET 10 SDK installed (`dotnet --version` → `10.x.x`)
- Visual Studio 2022 or VS Code with C# Dev Kit
- Access to an SAP B1 instance with Service Layer enabled
- SAP B1 Service Layer base URL (e.g., `https://your-server:50000`)

---

## Phase 1 — Solution Scaffold

### Step 1.1 — Create the solution and folder structure

```bash
cd "SBO projects/Service Layer"

# Create solution
dotnet new sln -n ServiceLayer.DTW

# Create src and tests folders
mkdir -p src tests

# Create all projects
dotnet new classlib -n ServiceLayer.DTW.Domain         -o src/ServiceLayer.DTW.Domain         -f net10.0
dotnet new classlib -n ServiceLayer.DTW.Application    -o src/ServiceLayer.DTW.Application    -f net10.0
dotnet new classlib -n ServiceLayer.DTW.Infrastructure -o src/ServiceLayer.DTW.Infrastructure -f net10.0
dotnet new classlib -n ServiceLayer.DTW.Shared         -o src/ServiceLayer.DTW.Shared         -f net10.0
dotnet new webapi   -n ServiceLayer.DTW.Api            -o src/ServiceLayer.DTW.Api            -f net10.0
dotnet new blazorwasm -n ServiceLayer.DTW.Web          -o src/ServiceLayer.DTW.Web            -f net10.0

# Create test projects
dotnet new xunit -n ServiceLayer.DTW.Domain.Tests         -o tests/ServiceLayer.DTW.Domain.Tests
dotnet new xunit -n ServiceLayer.DTW.Application.Tests    -o tests/ServiceLayer.DTW.Application.Tests
dotnet new xunit -n ServiceLayer.DTW.Infrastructure.Tests -o tests/ServiceLayer.DTW.Infrastructure.Tests
```

### Step 1.2 — Add all projects to the solution

```bash
dotnet sln ServiceLayer.DTW.sln add \
  src/ServiceLayer.DTW.Domain/ServiceLayer.DTW.Domain.csproj \
  src/ServiceLayer.DTW.Application/ServiceLayer.DTW.Application.csproj \
  src/ServiceLayer.DTW.Infrastructure/ServiceLayer.DTW.Infrastructure.csproj \
  src/ServiceLayer.DTW.Shared/ServiceLayer.DTW.Shared.csproj \
  src/ServiceLayer.DTW.Api/ServiceLayer.DTW.Api.csproj \
  src/ServiceLayer.DTW.Web/ServiceLayer.DTW.Web.csproj \
  tests/ServiceLayer.DTW.Domain.Tests/ServiceLayer.DTW.Domain.Tests.csproj \
  tests/ServiceLayer.DTW.Application.Tests/ServiceLayer.DTW.Application.Tests.csproj \
  tests/ServiceLayer.DTW.Infrastructure.Tests/ServiceLayer.DTW.Infrastructure.Tests.csproj
```

### Step 1.3 — Add project references (Clean Architecture dependency chain)

```bash
# Application depends on Domain
dotnet add src/ServiceLayer.DTW.Application reference src/ServiceLayer.DTW.Domain

# Infrastructure depends on Application
dotnet add src/ServiceLayer.DTW.Infrastructure reference src/ServiceLayer.DTW.Application

# Api depends on Application, Infrastructure, and Shared
dotnet add src/ServiceLayer.DTW.Api reference src/ServiceLayer.DTW.Application
dotnet add src/ServiceLayer.DTW.Api reference src/ServiceLayer.DTW.Infrastructure
dotnet add src/ServiceLayer.DTW.Api reference src/ServiceLayer.DTW.Shared

# Web (Blazor WASM) depends only on Shared (for DTOs)
dotnet add src/ServiceLayer.DTW.Web reference src/ServiceLayer.DTW.Shared

# Test projects reference their target + mocking libs
dotnet add tests/ServiceLayer.DTW.Application.Tests reference src/ServiceLayer.DTW.Application
dotnet add tests/ServiceLayer.DTW.Infrastructure.Tests reference src/ServiceLayer.DTW.Infrastructure
```

### Step 1.4 — Install NuGet packages

```bash
# Application layer
dotnet add src/ServiceLayer.DTW.Application package FluentValidation --version 11.*
dotnet add src/ServiceLayer.DTW.Application package MediatR --version 12.*

# Infrastructure layer
dotnet add src/ServiceLayer.DTW.Infrastructure package CsvHelper --version 33.*
dotnet add src/ServiceLayer.DTW.Infrastructure package EPPlus --version 7.*
dotnet add src/ServiceLayer.DTW.Infrastructure package Microsoft.Extensions.Http

# Api layer — Scalar replaces Swashbuckle in .NET 10
dotnet add src/ServiceLayer.DTW.Api package Microsoft.AspNetCore.OpenApi
dotnet add src/ServiceLayer.DTW.Api package Scalar.AspNetCore

# Test projects
dotnet add tests/ServiceLayer.DTW.Application.Tests package Moq
dotnet add tests/ServiceLayer.DTW.Application.Tests package FluentAssertions
dotnet add tests/ServiceLayer.DTW.Infrastructure.Tests package Moq
dotnet add tests/ServiceLayer.DTW.Infrastructure.Tests package FluentAssertions
```

### Step 1.5 — Delete auto-generated boilerplate files

Remove the default placeholder files created by the templates:
- `src/ServiceLayer.DTW.Domain/Class1.cs`
- `src/ServiceLayer.DTW.Application/Class1.cs`
- `src/ServiceLayer.DTW.Infrastructure/Class1.cs`
- `src/ServiceLayer.DTW.Shared/Class1.cs`
- `src/ServiceLayer.DTW.Api/WeatherForecast.cs`
- `src/ServiceLayer.DTW.Api/Controllers/WeatherForecastController.cs`

### Step 1.6 — Create the internal folder structure

Create these folders (add a `.gitkeep` or empty class to each so they are tracked):

```
src/ServiceLayer.DTW.Domain/
  Models/
  Enums/
  ValueObjects/
  Interfaces/

src/ServiceLayer.DTW.Application/
  UseCases/ImportBusinessPartners/
  Interfaces/
  DTOs/
  Validation/

src/ServiceLayer.DTW.Infrastructure/
  ServiceLayer/DTOs/
  Parsing/
  Mapping/

src/ServiceLayer.DTW.Shared/
  Requests/
  Responses/

src/ServiceLayer.DTW.Api/
  Controllers/

src/ServiceLayer.DTW.Web/
  Pages/
  Services/
  Shared/
```

### Step 1.7 — Verify the build

```bash
dotnet build ServiceLayer.DTW.sln
```

All projects should build with 0 errors.

---

## Phase 2 — Domain Layer

### Step 2.1 — Create enums

**`src/ServiceLayer.DTW.Domain/Enums/CardType.cs`**
```csharp
namespace ServiceLayer.DTW.Domain.Enums;

public enum CardType
{
    Customer,   // C
    Supplier,   // S
    Lead        // L
}
```

**`src/ServiceLayer.DTW.Domain/Enums/ImportMode.cs`**
```csharp
namespace ServiceLayer.DTW.Domain.Enums;

public enum ImportMode
{
    AddOnly,
    UpdateOnly,
    Upsert
}
```

**`src/ServiceLayer.DTW.Domain/Enums/ImportRowStatus.cs`**
```csharp
namespace ServiceLayer.DTW.Domain.Enums;

public enum ImportRowStatus
{
    Success,
    Error,
    Skipped
}
```

### Step 2.2 — Create domain entities

**`src/ServiceLayer.DTW.Domain/Models/BPAddress.cs`**
```csharp
namespace ServiceLayer.DTW.Domain.Models;

public class BPAddress
{
    public string AddressName { get; set; } = string.Empty;
    public string AddressType { get; set; } = string.Empty; // bo_BillTo / bo_ShipTo
    public string Street      { get; set; } = string.Empty;
    public string City        { get; set; } = string.Empty;
    public string ZipCode     { get; set; } = string.Empty;
    public string Country     { get; set; } = string.Empty;
    public string State       { get; set; } = string.Empty;
}
```

**`src/ServiceLayer.DTW.Domain/Models/ContactPerson.cs`**
```csharp
namespace ServiceLayer.DTW.Domain.Models;

public class ContactPerson
{
    public string Name        { get; set; } = string.Empty;
    public string FirstName   { get; set; } = string.Empty;
    public string LastName    { get; set; } = string.Empty;
    public string Phone1      { get; set; } = string.Empty;
    public string MobilePhone { get; set; } = string.Empty;
    public string Email       { get; set; } = string.Empty;
    public string Position    { get; set; } = string.Empty;
}
```

**`src/ServiceLayer.DTW.Domain/Models/BusinessPartner.cs`**
```csharp
using ServiceLayer.DTW.Domain.Enums;

namespace ServiceLayer.DTW.Domain.Models;

public class BusinessPartner
{
    public string   CardCode         { get; set; } = string.Empty;
    public string   CardName         { get; set; } = string.Empty;
    public CardType CardType         { get; set; }
    public int?     GroupCode        { get; set; }
    public string?  Currency         { get; set; }
    public int?     PayTermsGrpCode  { get; set; }
    public string?  Phone1           { get; set; }
    public string?  EmailAddress     { get; set; }
    public string?  Website          { get; set; }
    public string?  FederalTaxID     { get; set; }

    public List<BPAddress>     Addresses { get; set; } = [];
    public List<ContactPerson> Contacts  { get; set; } = [];
}
```

### Step 2.3 — Create domain interfaces (ports)

**`src/ServiceLayer.DTW.Domain/Interfaces/IImportRepository.cs`**
```csharp
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Domain.Models;

namespace ServiceLayer.DTW.Domain.Interfaces;

public interface IImportRepository
{
    Task AddAsync(BusinessPartner bp, CancellationToken ct = default);
    Task UpdateAsync(BusinessPartner bp, CancellationToken ct = default);
    Task<bool> ExistsAsync(string cardCode, CancellationToken ct = default);
}
```

---

## Phase 3 — Service Layer Auth Client

### Step 3.1 — Create configuration model

**`src/ServiceLayer.DTW.Infrastructure/ServiceLayer/ServiceLayerConfig.cs`**
```csharp
namespace ServiceLayer.DTW.Infrastructure.ServiceLayer;

public class ServiceLayerConfig
{
    public string BaseUrl   { get; set; } = string.Empty; // e.g. https://server:50000
    public string CompanyDb { get; set; } = string.Empty;
    public string UserName  { get; set; } = string.Empty;
    public string Password  { get; set; } = string.Empty;
}
```

### Step 3.2 — Create the Application port (interface)

**`src/ServiceLayer.DTW.Application/Interfaces/IServiceLayerClient.cs`**
```csharp
using ServiceLayer.DTW.Domain.Models;

namespace ServiceLayer.DTW.Application.Interfaces;

public interface IServiceLayerClient
{
    Task LoginAsync(CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task CreateBusinessPartnerAsync(BusinessPartner bp, CancellationToken ct = default);
    Task UpdateBusinessPartnerAsync(BusinessPartner bp, CancellationToken ct = default);
    Task<bool> BusinessPartnerExistsAsync(string cardCode, CancellationToken ct = default);
}
```

### Step 3.3 — Create SL-specific DTOs (Infrastructure layer only)

**`src/ServiceLayer.DTW.Infrastructure/ServiceLayer/DTOs/SLBPAddress.cs`**
```csharp
using System.Text.Json.Serialization;

namespace ServiceLayer.DTW.Infrastructure.ServiceLayer.DTOs;

public class SLBPAddress
{
    [JsonPropertyName("AddressName")] public string AddressName { get; set; } = string.Empty;
    [JsonPropertyName("AddressType")] public string AddressType { get; set; } = string.Empty;
    [JsonPropertyName("Street")]      public string Street      { get; set; } = string.Empty;
    [JsonPropertyName("City")]        public string City        { get; set; } = string.Empty;
    [JsonPropertyName("ZipCode")]     public string ZipCode     { get; set; } = string.Empty;
    [JsonPropertyName("Country")]     public string Country     { get; set; } = string.Empty;
    [JsonPropertyName("State")]       public string State       { get; set; } = string.Empty;
}
```

**`src/ServiceLayer.DTW.Infrastructure/ServiceLayer/DTOs/SLContactEmployee.cs`**
```csharp
using System.Text.Json.Serialization;

namespace ServiceLayer.DTW.Infrastructure.ServiceLayer.DTOs;

public class SLContactEmployee
{
    [JsonPropertyName("Name")]        public string Name        { get; set; } = string.Empty;
    [JsonPropertyName("FirstName")]   public string FirstName   { get; set; } = string.Empty;
    [JsonPropertyName("LastName")]    public string LastName    { get; set; } = string.Empty;
    [JsonPropertyName("Phone1")]      public string Phone1      { get; set; } = string.Empty;
    [JsonPropertyName("MobilePhone")] public string MobilePhone { get; set; } = string.Empty;
    [JsonPropertyName("E_Mail")]      public string Email       { get; set; } = string.Empty;
    [JsonPropertyName("Position")]    public string Position    { get; set; } = string.Empty;
}
```

**`src/ServiceLayer.DTW.Infrastructure/ServiceLayer/DTOs/SLBusinessPartner.cs`**
```csharp
using System.Text.Json.Serialization;

namespace ServiceLayer.DTW.Infrastructure.ServiceLayer.DTOs;

public class SLBusinessPartner
{
    [JsonPropertyName("CardCode")]        public string  CardCode        { get; set; } = string.Empty;
    [JsonPropertyName("CardName")]        public string  CardName        { get; set; } = string.Empty;
    [JsonPropertyName("CardType")]        public string  CardType        { get; set; } = string.Empty;
    [JsonPropertyName("GroupCode")]       public int?    GroupCode       { get; set; }
    [JsonPropertyName("Currency")]        public string? Currency        { get; set; }
    [JsonPropertyName("PayTermsGrpCode")] public int?    PayTermsGrpCode { get; set; }
    [JsonPropertyName("Phone1")]          public string? Phone1          { get; set; }
    [JsonPropertyName("EmailAddress")]    public string? EmailAddress    { get; set; }
    [JsonPropertyName("Website")]         public string? Website         { get; set; }
    [JsonPropertyName("FederalTaxID")]    public string? FederalTaxID    { get; set; }

    [JsonPropertyName("BPAddresses")]
    public List<SLBPAddress> BPAddresses { get; set; } = [];

    [JsonPropertyName("ContactEmployees")]
    public List<SLContactEmployee> ContactEmployees { get; set; } = [];
}
```

### Step 3.4 — Implement the Service Layer client

**`src/ServiceLayer.DTW.Infrastructure/ServiceLayer/ServiceLayerClient.cs`**
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServiceLayer.DTW.Application.Interfaces;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Domain.Models;
using ServiceLayer.DTW.Infrastructure.ServiceLayer.DTOs;
using ServiceLayer.DTW.Infrastructure.Mapping;

namespace ServiceLayer.DTW.Infrastructure.ServiceLayer;

public class ServiceLayerClient : IServiceLayerClient
{
    private readonly HttpClient          _http;
    private readonly ServiceLayerConfig  _config;

    public ServiceLayerClient(HttpClient http, IOptions<ServiceLayerConfig> config)
    {
        _http   = http;
        _config = config.Value;
    }

    public async Task LoginAsync(CancellationToken ct = default)
    {
        var payload = new
        {
            CompanyDB = _config.CompanyDb,
            UserName  = _config.UserName,
            Password  = _config.Password
        };

        var response = await _http.PostAsJsonAsync("Login", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        await _http.PostAsync("Logout", null, ct);
    }

    public async Task CreateBusinessPartnerAsync(BusinessPartner bp, CancellationToken ct = default)
    {
        var dto      = BusinessPartnerMapper.ToSLDto(bp);
        var response = await _http.PostAsJsonAsync("BusinessPartners", dto, ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadErrorMessage(response));
    }

    public async Task UpdateBusinessPartnerAsync(BusinessPartner bp, CancellationToken ct = default)
    {
        var dto      = BusinessPartnerMapper.ToSLDto(bp);
        var response = await _http.PatchAsJsonAsync($"BusinessPartners('{bp.CardCode}')", dto, ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadErrorMessage(response));
    }

    public async Task<bool> BusinessPartnerExistsAsync(string cardCode, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(
            $"BusinessPartners('{cardCode}')?$select=CardCode", ct);

        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    private static async Task<string> ReadErrorMessage(HttpResponseMessage response)
    {
        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("error").GetProperty("message").GetProperty("value").GetString()
                   ?? response.ReasonPhrase
                   ?? "Unknown error";
        }
        catch
        {
            return response.ReasonPhrase ?? "Unknown error";
        }
    }
}
```

### Step 3.5 — Configure `appsettings.json` in the API

**`src/ServiceLayer.DTW.Api/appsettings.json`** — add the SL section:
```json
{
  "ServiceLayer": {
    "BaseUrl":   "https://your-server:50000/b1s/v1/",
    "CompanyDb": "YourCompanyDB",
    "UserName":  "manager",
    "Password":  "yourpassword"
  },
  "Logging": {
    "LogLevel": {
      "Default":               "Information",
      "Microsoft.AspNetCore":  "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

---

## Phase 4 — File Parsers

### Step 4.1 — Create the parser port (Application layer)

**`src/ServiceLayer.DTW.Application/Interfaces/IFileParser.cs`**
```csharp
using ServiceLayer.DTW.Application.DTOs;

namespace ServiceLayer.DTW.Application.Interfaces;

public interface IFileParser
{
    /// <summary>
    /// Parses a file stream into a list of rows.
    /// Each row is a dictionary of column name → value.
    /// </summary>
    Task<List<ParsedRowDto>> ParseAsync(Stream stream, string fileName, CancellationToken ct = default);
    bool Supports(string fileName);
}
```

### Step 4.2 — Create the ParsedRowDto

**`src/ServiceLayer.DTW.Application/DTOs/ParsedRowDto.cs`**
```csharp
namespace ServiceLayer.DTW.Application.DTOs;

public class ParsedRowDto
{
    public int                        RowNumber { get; set; }
    public Dictionary<string, string> Fields    { get; set; } = [];
}
```

### Step 4.3 — Implement the CSV parser

**`src/ServiceLayer.DTW.Infrastructure/Parsing/CsvParser.cs`**
```csharp
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ServiceLayer.DTW.Application.DTOs;
using ServiceLayer.DTW.Application.Interfaces;

namespace ServiceLayer.DTW.Infrastructure.Parsing;

public class CsvParser : IFileParser
{
    public bool Supports(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".csv" or ".txt";
    }

    public Task<List<ParsedRowDto>> ParseAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord  = true,
            TrimOptions      = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound     = null
        };

        using var reader = new StreamReader(stream);
        using var csv    = new CsvHelper.CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];

        var rows   = new List<ParsedRowDto>();
        int rowNum = 1;

        while (csv.Read())
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in headers)
                fields[header] = csv.GetField(header) ?? string.Empty;

            rows.Add(new ParsedRowDto { RowNumber = rowNum++, Fields = fields });
        }

        return Task.FromResult(rows);
    }
}
```

### Step 4.4 — Implement the Excel parser

**`src/ServiceLayer.DTW.Infrastructure/Parsing/ExcelParser.cs`**
```csharp
using OfficeOpenXml;
using ServiceLayer.DTW.Application.DTOs;
using ServiceLayer.DTW.Application.Interfaces;

namespace ServiceLayer.DTW.Infrastructure.Parsing;

public class ExcelParser : IFileParser
{
    public bool Supports(string fileName) =>
        Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    public Task<List<ParsedRowDto>> ParseAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package   = new ExcelPackage(stream);
        var       worksheet = package.Workbook.Worksheets.First();

        int colCount = worksheet.Dimension?.Columns ?? 0;
        int rowCount = worksheet.Dimension?.Rows    ?? 0;

        if (colCount == 0 || rowCount < 2)
            return Task.FromResult(new List<ParsedRowDto>());

        // First row = headers
        var headers = Enumerable.Range(1, colCount)
            .Select(c => worksheet.Cells[1, c].Text.Trim())
            .ToArray();

        var rows = new List<ParsedRowDto>();

        for (int r = 2; r <= rowCount; r++)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int c = 1; c <= colCount; c++)
                fields[headers[c - 1]] = worksheet.Cells[r, c].Text.Trim();

            rows.Add(new ParsedRowDto { RowNumber = r - 1, Fields = fields });
        }

        return Task.FromResult(rows);
    }
}
```

### Step 4.5 — Create a parser resolver

**`src/ServiceLayer.DTW.Infrastructure/Parsing/FileParserResolver.cs`**
```csharp
using ServiceLayer.DTW.Application.Interfaces;

namespace ServiceLayer.DTW.Infrastructure.Parsing;

public class FileParserResolver
{
    private readonly IEnumerable<IFileParser> _parsers;

    public FileParserResolver(IEnumerable<IFileParser> parsers)
        => _parsers = parsers;

    public IFileParser Resolve(string fileName) =>
        _parsers.FirstOrDefault(p => p.Supports(fileName))
        ?? throw new NotSupportedException($"No parser found for file: {fileName}");
}
```

---

## Phase 5 — Mapper and Validator

### Step 5.1 — Implement the Business Partner mapper

**`src/ServiceLayer.DTW.Infrastructure/Mapping/BusinessPartnerMapper.cs`**
```csharp
using ServiceLayer.DTW.Application.DTOs;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Domain.Models;
using ServiceLayer.DTW.Infrastructure.ServiceLayer.DTOs;

namespace ServiceLayer.DTW.Infrastructure.Mapping;

public static class BusinessPartnerMapper
{
    // Map ParsedRowDto (from file) → Domain BusinessPartner
    public static BusinessPartner ToDomain(ParsedRowDto row)
    {
        var f = row.Fields;

        return new BusinessPartner
        {
            CardCode        = f.GetValueOrDefault("CardCode",        string.Empty),
            CardName        = f.GetValueOrDefault("CardName",        string.Empty),
            CardType        = ParseCardType(f.GetValueOrDefault("CardType", "C")),
            GroupCode       = ParseNullableInt(f.GetValueOrDefault("GroupCode")),
            Currency        = f.GetValueOrDefault("Currency"),
            PayTermsGrpCode = ParseNullableInt(f.GetValueOrDefault("PayTermsGrpCode")),
            Phone1          = f.GetValueOrDefault("Phone1"),
            EmailAddress    = f.GetValueOrDefault("EmailAddress"),
            Website         = f.GetValueOrDefault("Website"),
            FederalTaxID    = f.GetValueOrDefault("FederalTaxID")
        };
    }

    // Map Domain BusinessPartner → SL DTO (for HTTP requests)
    public static SLBusinessPartner ToSLDto(BusinessPartner bp) => new()
    {
        CardCode        = bp.CardCode,
        CardName        = bp.CardName,
        CardType        = CardTypeToSLString(bp.CardType),
        GroupCode       = bp.GroupCode,
        Currency        = bp.Currency,
        PayTermsGrpCode = bp.PayTermsGrpCode,
        Phone1          = bp.Phone1,
        EmailAddress    = bp.EmailAddress,
        Website         = bp.Website,
        FederalTaxID    = bp.FederalTaxID,

        BPAddresses = bp.Addresses.Select(a => new SLBPAddress
        {
            AddressName = a.AddressName,
            AddressType = a.AddressType,
            Street      = a.Street,
            City        = a.City,
            ZipCode     = a.ZipCode,
            Country     = a.Country,
            State       = a.State
        }).ToList(),

        ContactEmployees = bp.Contacts.Select(c => new SLContactEmployee
        {
            Name        = c.Name,
            FirstName   = c.FirstName,
            LastName    = c.LastName,
            Phone1      = c.Phone1,
            MobilePhone = c.MobilePhone,
            Email       = c.Email,
            Position    = c.Position
        }).ToList()
    };

    private static CardType ParseCardType(string value) => value.ToUpperInvariant() switch
    {
        "C" or "CUSTOMER" => CardType.Customer,
        "S" or "SUPPLIER" => CardType.Supplier,
        "L" or "LEAD"     => CardType.Lead,
        _                  => CardType.Customer
    };

    private static string CardTypeToSLString(CardType ct) => ct switch
    {
        CardType.Customer => "cCustomer",
        CardType.Supplier => "cSupplier",
        CardType.Lead     => "cLead",
        _                  => "cCustomer"
    };

    private static int? ParseNullableInt(string? value) =>
        int.TryParse(value, out var result) ? result : null;
}
```

### Step 5.2 — Implement the FluentValidation validator

**`src/ServiceLayer.DTW.Application/Validation/BusinessPartnerValidator.cs`**
```csharp
using FluentValidation;
using ServiceLayer.DTW.Domain.Models;

namespace ServiceLayer.DTW.Application.Validation;

public class BusinessPartnerValidator : AbstractValidator<BusinessPartner>
{
    public BusinessPartnerValidator()
    {
        RuleFor(bp => bp.CardCode)
            .NotEmpty().WithMessage("CardCode is required.")
            .MaximumLength(15).WithMessage("CardCode cannot exceed 15 characters.");

        RuleFor(bp => bp.CardName)
            .NotEmpty().WithMessage("CardName is required.")
            .MaximumLength(100).WithMessage("CardName cannot exceed 100 characters.");

        RuleFor(bp => bp.EmailAddress)
            .EmailAddress().When(bp => !string.IsNullOrWhiteSpace(bp.EmailAddress))
            .WithMessage("EmailAddress is not valid.");

        RuleForEach(bp => bp.Addresses).ChildRules(addr =>
        {
            addr.RuleFor(a => a.AddressType)
                .Must(t => t is "bo_BillTo" or "bo_ShipTo")
                .WithMessage("AddressType must be 'bo_BillTo' or 'bo_ShipTo'.");
        });
    }
}
```

---

## Phase 6 — Application Use Case

### Step 6.1 — Create the command and result

**`src/ServiceLayer.DTW.Application/UseCases/ImportBusinessPartners/ImportBusinessPartnersCommand.cs`**
```csharp
using MediatR;
using ServiceLayer.DTW.Domain.Enums;

namespace ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;

public record ImportBusinessPartnersCommand(
    Stream   FileStream,
    string   FileName,
    ImportMode Mode,
    bool     StopOnError
) : IRequest<ImportBusinessPartnersResult>;
```

**`src/ServiceLayer.DTW.Application/UseCases/ImportBusinessPartners/ImportBusinessPartnersResult.cs`**
```csharp
using ServiceLayer.DTW.Domain.Enums;

namespace ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;

public class ImportBusinessPartnersResult
{
    public int                       TotalRows    { get; set; }
    public int                       SuccessCount { get; set; }
    public int                       ErrorCount   { get; set; }
    public int                       SkippedCount { get; set; }
    public List<ImportRowResult>     RowResults   { get; set; } = [];
}

public class ImportRowResult
{
    public int             RowNumber { get; set; }
    public string          CardCode  { get; set; } = string.Empty;
    public ImportRowStatus Status    { get; set; }
    public string?         Message   { get; set; }
}
```

### Step 6.2 — Implement the MediatR handler

**`src/ServiceLayer.DTW.Application/UseCases/ImportBusinessPartners/ImportBusinessPartnersHandler.cs`**
```csharp
using FluentValidation;
using MediatR;
using ServiceLayer.DTW.Application.Interfaces;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Domain.Interfaces;

namespace ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;

public class ImportBusinessPartnersHandler
    : IRequestHandler<ImportBusinessPartnersCommand, ImportBusinessPartnersResult>
{
    private readonly IFileParser           _parser;        // resolved externally per file type
    private readonly IServiceLayerClient   _slClient;
    private readonly IValidator<Domain.Models.BusinessPartner> _validator;

    // NOTE: In Phase 7, the parser will be resolved by a resolver.
    // For now, inject IFileParser directly (or use the resolver).
    public ImportBusinessPartnersHandler(
        IFileParser                                     parser,
        IServiceLayerClient                             slClient,
        IValidator<Domain.Models.BusinessPartner>       validator)
    {
        _parser    = parser;
        _slClient  = slClient;
        _validator = validator;
    }

    public async Task<ImportBusinessPartnersResult> Handle(
        ImportBusinessPartnersCommand command,
        CancellationToken             ct)
    {
        var result = new ImportBusinessPartnersResult();

        // 1. Parse file
        var rows = await _parser.ParseAsync(command.FileStream, command.FileName, ct);
        result.TotalRows = rows.Count;

        // 2. Login to Service Layer
        await _slClient.LoginAsync(ct);

        try
        {
            foreach (var row in rows)
            {
                var rowResult = new ImportRowResult { RowNumber = row.RowNumber };
                result.RowResults.Add(rowResult);

                // 3. Map to domain entity
                // Mapping happens in Infrastructure; use interface here
                // (BusinessPartnerMapper is called by a domain service or adapter)
                // For simplicity in this phase, we import a mapped object
                var bp = MapRow(row);
                rowResult.CardCode = bp.CardCode;

                // 4. Validate
                var validation = await _validator.ValidateAsync(bp, ct);
                if (!validation.IsValid)
                {
                    rowResult.Status  = ImportRowStatus.Error;
                    rowResult.Message = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                    result.ErrorCount++;

                    if (command.StopOnError) break;
                    continue;
                }

                // 5. Import based on mode
                try
                {
                    bool exists = command.Mode != ImportMode.AddOnly
                        && await _slClient.BusinessPartnerExistsAsync(bp.CardCode, ct);

                    if (command.Mode == ImportMode.AddOnly && exists)
                    {
                        rowResult.Status  = ImportRowStatus.Skipped;
                        rowResult.Message = "CardCode already exists (AddOnly mode).";
                        result.SkippedCount++;
                        continue;
                    }

                    if (command.Mode == ImportMode.UpdateOnly && !exists)
                    {
                        rowResult.Status  = ImportRowStatus.Skipped;
                        rowResult.Message = "CardCode not found (UpdateOnly mode).";
                        result.SkippedCount++;
                        continue;
                    }

                    if (exists)
                        await _slClient.UpdateBusinessPartnerAsync(bp, ct);
                    else
                        await _slClient.CreateBusinessPartnerAsync(bp, ct);

                    rowResult.Status = ImportRowStatus.Success;
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    rowResult.Status  = ImportRowStatus.Error;
                    rowResult.Message = ex.Message;
                    result.ErrorCount++;

                    if (command.StopOnError) break;
                }
            }
        }
        finally
        {
            await _slClient.LogoutAsync(ct);
        }

        return result;
    }

    // Temporary inline mapping — will be replaced by injected mapper in Phase 7
    private static Domain.Models.BusinessPartner MapRow(Application.DTOs.ParsedRowDto row)
    {
        var f = row.Fields;
        return new Domain.Models.BusinessPartner
        {
            CardCode     = f.GetValueOrDefault("CardCode",  string.Empty),
            CardName     = f.GetValueOrDefault("CardName",  string.Empty),
            EmailAddress = f.GetValueOrDefault("EmailAddress"),
            Phone1       = f.GetValueOrDefault("Phone1"),
            FederalTaxID = f.GetValueOrDefault("FederalTaxID")
        };
    }
}
```

---

## Phase 7 — Shared DTOs and API

### Step 7.1 — Create Shared request/response models

**`src/ServiceLayer.DTW.Shared/Requests/StartImportRequest.cs`**
```csharp
using ServiceLayer.DTW.Domain.Enums;

namespace ServiceLayer.DTW.Shared.Requests;

public class StartImportRequest
{
    public ImportMode Mode        { get; set; } = ImportMode.Upsert;
    public bool       StopOnError { get; set; } = false;
}
```

**`src/ServiceLayer.DTW.Shared/Responses/ImportRowResultResponse.cs`**
```csharp
using ServiceLayer.DTW.Domain.Enums;

namespace ServiceLayer.DTW.Shared.Responses;

public class ImportRowResultResponse
{
    public int             RowNumber { get; set; }
    public string          CardCode  { get; set; } = string.Empty;
    public ImportRowStatus Status    { get; set; }
    public string?         Message   { get; set; }
}
```

**`src/ServiceLayer.DTW.Shared/Responses/ImportJobResponse.cs`**
```csharp
namespace ServiceLayer.DTW.Shared.Responses;

public class ImportJobResponse
{
    public int                          TotalRows    { get; set; }
    public int                          SuccessCount { get; set; }
    public int                          ErrorCount   { get; set; }
    public int                          SkippedCount { get; set; }
    public List<ImportRowResultResponse> RowResults  { get; set; } = [];
}
```

### Step 7.2 — Create the Import API controller

**`src/ServiceLayer.DTW.Api/Controllers/ImportController.cs`**
```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Shared.Responses;

namespace ServiceLayer.DTW.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly IMediator _mediator;

    public ImportController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Upload a CSV/TXT/XLSX file to import Business Partners.
    /// </summary>
    [HttpPost("business-partners")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportJobResponse>> ImportBusinessPartners(
        IFormFile  file,
        [FromForm] ImportMode mode        = ImportMode.Upsert,
        [FromForm] bool       stopOnError = false,
        CancellationToken     ct          = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        await using var stream = file.OpenReadStream();

        var command = new ImportBusinessPartnersCommand(stream, file.FileName, mode, stopOnError);
        var result  = await _mediator.Send(command, ct);

        var response = new ImportJobResponse
        {
            TotalRows    = result.TotalRows,
            SuccessCount = result.SuccessCount,
            ErrorCount   = result.ErrorCount,
            SkippedCount = result.SkippedCount,
            RowResults   = result.RowResults.Select(r => new ImportRowResultResponse
            {
                RowNumber = r.RowNumber,
                CardCode  = r.CardCode,
                Status    = r.Status,
                Message   = r.Message
            }).ToList()
        };

        return Ok(response);
    }
}
```

### Step 7.3 — Configure dependency injection in Program.cs

**`src/ServiceLayer.DTW.Api/Program.cs`**
```csharp
using FluentValidation;
using ServiceLayer.DTW.Application.Interfaces;
using ServiceLayer.DTW.Application.Validation;
using ServiceLayer.DTW.Domain.Models;
using ServiceLayer.DTW.Infrastructure.Mapping;
using ServiceLayer.DTW.Infrastructure.Parsing;
using ServiceLayer.DTW.Infrastructure.ServiceLayer;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
builder.Services.Configure<ServiceLayerConfig>(
    builder.Configuration.GetSection("ServiceLayer"));

// --- MediatR ---
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners
                                .ImportBusinessPartnersHandler).Assembly));

// --- FluentValidation ---
builder.Services.AddScoped<IValidator<BusinessPartner>, BusinessPartnerValidator>();

// --- File Parsers ---
builder.Services.AddScoped<IFileParser, CsvParser>();
builder.Services.AddScoped<IFileParser, ExcelParser>();
builder.Services.AddScoped<FileParserResolver>();

// --- Service Layer HTTP Client ---
builder.Services.AddHttpClient<IServiceLayerClient, ServiceLayerClient>(client =>
{
    var config = builder.Configuration.GetSection("ServiceLayer").Get<ServiceLayerConfig>()!;
    client.BaseAddress = new Uri(config.BaseUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Accept self-signed certificates on dev SAP servers
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    UseCookies = true,
    CookieContainer = new System.Net.CookieContainer()
});

// --- CORS (for Blazor WASM) ---
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("https://localhost:7001")
              .AllowAnyHeader()
              .AllowAnyMethod()));

// --- Controllers & OpenAPI (Scalar) ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();          // serves /openapi/v1.json
    app.MapScalarApiReference(); // serves /scalar/v1
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();
app.Run();
```

---

## Phase 8 — Blazor WASM UI

### Step 8.1 — Create the API client service

**`src/ServiceLayer.DTW.Web/Services/ImportApiClient.cs`**
```csharp
using System.Net.Http.Json;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Shared.Responses;

namespace ServiceLayer.DTW.Web.Services;

public class ImportApiClient
{
    private readonly HttpClient _http;

    public ImportApiClient(HttpClient http) => _http = http;

    public async Task<ImportJobResponse?> ImportBusinessPartnersAsync(
        Stream     fileStream,
        string     fileName,
        ImportMode mode,
        bool       stopOnError)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        content.Add(new StringContent(mode.ToString()),        "mode");
        content.Add(new StringContent(stopOnError.ToString()), "stopOnError");

        var response = await _http.PostAsync("api/import/business-partners", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportJobResponse>();
    }
}
```

### Step 8.2 — Register HTTP client in Blazor WASM

**`src/ServiceLayer.DTW.Web/Program.cs`**
```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ServiceLayer.DTW.Web;
using ServiceLayer.DTW.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7000/") // API base URL
});

builder.Services.AddScoped<ImportApiClient>();

await builder.Build().RunAsync();
```

### Step 8.3 — Create the Import page

**`src/ServiceLayer.DTW.Web/Pages/Import.razor`**
```razor
@page "/import"
@using ServiceLayer.DTW.Domain.Enums
@using ServiceLayer.DTW.Shared.Responses
@using ServiceLayer.DTW.Web.Services
@inject ImportApiClient ApiClient

<h2>Import Business Partners</h2>

<div>
    <label>File (CSV / TXT / XLSX):</label>
    <InputFile OnChange="OnFileSelected" accept=".csv,.txt,.xlsx" />
</div>

<div>
    <label>Import Mode:</label>
    <select @bind="_mode">
        <option value="@ImportMode.Upsert">Upsert (Add or Update)</option>
        <option value="@ImportMode.AddOnly">Add Only</option>
        <option value="@ImportMode.UpdateOnly">Update Only</option>
    </select>
</div>

<div>
    <label>
        <input type="checkbox" @bind="_stopOnError" />
        Stop on first error
    </label>
</div>

<button @onclick="RunImport" disabled="@(_file is null || _importing)">
    @(_importing ? "Importing..." : "Start Import")
</button>

@if (_result is not null)
{
    <hr />
    <h3>Results</h3>
    <p>Total: @_result.TotalRows | Success: @_result.SuccessCount | Errors: @_result.ErrorCount | Skipped: @_result.SkippedCount</p>

    <table>
        <thead>
            <tr>
                <th>Row</th><th>CardCode</th><th>Status</th><th>Message</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var row in _result.RowResults)
            {
                <tr>
                    <td>@row.RowNumber</td>
                    <td>@row.CardCode</td>
                    <td>@row.Status</td>
                    <td>@row.Message</td>
                </tr>
            }
        </tbody>
    </table>
}

@if (_error is not null)
{
    <p style="color:red">@_error</p>
}

@code {
    private IBrowserFile? _file;
    private ImportMode    _mode        = ImportMode.Upsert;
    private bool          _stopOnError = false;
    private bool          _importing   = false;
    private ImportJobResponse? _result;
    private string?       _error;

    private void OnFileSelected(InputFileChangeEventArgs e)
        => _file = e.File;

    private async Task RunImport()
    {
        if (_file is null) return;
        _importing = true;
        _result    = null;
        _error     = null;

        try
        {
            await using var stream = _file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            _result = await ApiClient.ImportBusinessPartnersAsync(
                stream, _file.Name, _mode, _stopOnError);
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _importing = false;
        }
    }
}
```

---

## Phase 9 — Result Log Export

### Step 9.1 — Add CSV export endpoint to the API

**`src/ServiceLayer.DTW.Api/Controllers/ImportController.cs`** — add this action:
```csharp
[HttpPost("business-partners/export-log")]
public IActionResult ExportLog([FromBody] ImportJobResponse result)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("RowNumber,CardCode,Status,Message");

    foreach (var row in result.RowResults)
    {
        var msg = row.Message?.Replace("\"", "\"\"") ?? string.Empty;
        sb.AppendLine($"{row.RowNumber},{row.CardCode},{row.Status},\"{msg}\"");
    }

    var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    return File(bytes, "text/csv", "import-results.csv");
}
```

---

## Phase 10 — Testing

### Step 10.1 — Unit test the validator

**`tests/ServiceLayer.DTW.Application.Tests/BusinessPartnerValidatorTests.cs`**
```csharp
using FluentAssertions;
using ServiceLayer.DTW.Application.Validation;
using ServiceLayer.DTW.Domain.Models;

namespace ServiceLayer.DTW.Application.Tests;

public class BusinessPartnerValidatorTests
{
    private readonly BusinessPartnerValidator _sut = new();

    [Fact]
    public async Task Should_Fail_When_CardCode_Is_Empty()
    {
        var bp = new BusinessPartner { CardCode = "", CardName = "Test" };
        var result = await _sut.ValidateAsync(bp);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CardCode");
    }

    [Fact]
    public async Task Should_Fail_When_Email_Is_Invalid()
    {
        var bp = new BusinessPartner { CardCode = "C001", CardName = "Test", EmailAddress = "not-an-email" };
        var result = await _sut.ValidateAsync(bp);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EmailAddress");
    }

    [Fact]
    public async Task Should_Pass_For_Valid_Business_Partner()
    {
        var bp = new BusinessPartner { CardCode = "C001", CardName = "Acme Corp", EmailAddress = "info@acme.com" };
        var result = await _sut.ValidateAsync(bp);
        result.IsValid.Should().BeTrue();
    }
}
```

### Step 10.2 — Unit test the CSV parser

**`tests/ServiceLayer.DTW.Infrastructure.Tests/CsvParserTests.cs`**
```csharp
using System.Text;
using FluentAssertions;
using ServiceLayer.DTW.Infrastructure.Parsing;

namespace ServiceLayer.DTW.Infrastructure.Tests;

public class CsvParserTests
{
    private readonly CsvParser _sut = new();

    [Fact]
    public async Task Should_Parse_Csv_Rows_Correctly()
    {
        const string csv = "CardCode,CardName,CardType\nC001,Acme Corp,C\nS001,Big Supplier,S";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rows = await _sut.ParseAsync(stream, "test.csv");

        rows.Should().HaveCount(2);
        rows[0].Fields["CardCode"].Should().Be("C001");
        rows[1].Fields["CardType"].Should().Be("S");
    }

    [Theory]
    [InlineData("file.csv")]
    [InlineData("file.txt")]
    public void Should_Support_Csv_And_Txt(string fileName)
        => _sut.Supports(fileName).Should().BeTrue();

    [Fact]
    public void Should_Not_Support_Xlsx()
        => _sut.Supports("file.xlsx").Should().BeFalse();
}
```

### Step 10.3 — Unit test the handler (with mocks)

**`tests/ServiceLayer.DTW.Application.Tests/ImportBusinessPartnersHandlerTests.cs`**
```csharp
using System.Text;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using ServiceLayer.DTW.Application.Interfaces;
using ServiceLayer.DTW.Application.DTOs;
using ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Domain.Models;

namespace ServiceLayer.DTW.Application.Tests;

public class ImportBusinessPartnersHandlerTests
{
    [Fact]
    public async Task Should_Return_Success_For_Valid_Row()
    {
        var parserMock    = new Mock<IFileParser>();
        var slClientMock  = new Mock<IServiceLayerClient>();
        var validatorMock = new Mock<IValidator<BusinessPartner>>();

        parserMock.Setup(p => p.Supports(It.IsAny<string>())).Returns(true);
        parserMock.Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<string>(), default))
            .ReturnsAsync(new List<ParsedRowDto>
            {
                new() { RowNumber = 1, Fields = new() {
                    { "CardCode", "C001" }, { "CardName", "Acme" }
                }}
            });

        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<BusinessPartner>(), default))
            .ReturnsAsync(new ValidationResult());

        slClientMock.Setup(s => s.BusinessPartnerExistsAsync("C001", default)).ReturnsAsync(false);

        var handler = new ImportBusinessPartnersHandler(
            parserMock.Object, slClientMock.Object, validatorMock.Object);

        var command = new ImportBusinessPartnersCommand(
            new MemoryStream(), "test.csv", ImportMode.Upsert, false);

        var result = await handler.Handle(command, default);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
    }
}
```

### Step 10.4 — Run all tests

```bash
dotnet test ServiceLayer.DTW.sln
```

---

## Phase 11 — Run and Verify

### Step 11.1 — Run the API

```bash
dotnet run --project src/ServiceLayer.DTW.Api
```

Navigate to `https://localhost:7000/scalar/v1` to test the endpoint interactively via Scalar UI.

### Step 11.2 — Run the Blazor WASM app

```bash
dotnet run --project src/ServiceLayer.DTW.Web
```

Navigate to `https://localhost:7001/import`.

### Step 11.3 — Sample BP import file (CSV)

Create a `sample-bp.csv` file:
```
CardCode,CardName,CardType,Phone1,EmailAddress,FederalTaxID
C001,Acme Corporation,C,+1-555-0100,info@acme.com,12-3456789
S001,Big Supplier Ltd,S,+1-555-0200,orders@bigsupplier.com,98-7654321
C002,New Lead Co,L,,leads@newlead.com,
```

Upload this file via the Scalar UI (`/scalar/v1`) or the Blazor UI to verify end-to-end import.

---

## Reference — Template Column Names

### Business Partner Header Template

| Column Name      | Required | Notes                                |
|------------------|----------|--------------------------------------|
| `CardCode`       | Yes      | Max 15 chars                         |
| `CardName`       | Yes      | Max 100 chars                        |
| `CardType`       | Yes      | `C` / `S` / `L`                     |
| `GroupCode`      | No       | Integer                              |
| `Currency`       | No       | e.g., `USD`, `EUR`                   |
| `PayTermsGrpCode`| No       | Integer                              |
| `Phone1`         | No       |                                      |
| `EmailAddress`   | No       | Validated format                     |
| `Website`        | No       |                                      |
| `FederalTaxID`   | No       |                                      |

### Address Template (per row, linked by CardCode)

| Column Name   | Notes                         |
|---------------|-------------------------------|
| `CardCode`    | Links to BP header            |
| `AddressName` | e.g., `Bill To`, `Ship To`   |
| `AddressType` | `bo_BillTo` or `bo_ShipTo`  |
| `Street`      |                               |
| `City`        |                               |
| `ZipCode`     |                               |
| `Country`     | Two-letter ISO code           |
| `State`       |                               |

---

## Common Issues and Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| SSL error connecting to SL | Self-signed cert on SAP server | Use `DangerousAcceptAnyServerCertificateValidator` in dev; use a valid cert in production |
| `401 Unauthorized` from SL | Session expired or login failed | Ensure cookie container is shared in `HttpClientHandler`; call `LoginAsync` before each batch |
| `400 Bad Request` from SL | Missing required field or wrong value | Check SL error message in `error.message.value`; review CardType string (`cCustomer` not `Customer`) |
| Large file upload rejected | Blazor default max file size is 512KB | Set `maxAllowedSize` in `OpenReadStream()` |
| CORS error in browser | API not allowing Blazor origin | Configure `WithOrigins` in `AddCors` in `Program.cs` |
