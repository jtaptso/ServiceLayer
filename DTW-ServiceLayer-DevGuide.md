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
  Assembly/
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

    /// <summary>Captures any U_* UDF columns from the address file automatically.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
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

    /// <summary>Captures any U_* UDF columns from the contacts file automatically.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
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

    /// <summary>Captures any U_* UDF columns from the header file automatically.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
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

### Step 4.3a — Create BPImportFilesDto (groups the 3 parsed file results)

**`src/ServiceLayer.DTW.Application/DTOs/BPImportFilesDto.cs`**
```csharp
namespace ServiceLayer.DTW.Application.DTOs;

/// <summary>
/// Holds the parsed rows from each of the 3 DTW-style template files.
/// AddressRows and ContactRows are optional.
/// </summary>
public class BPImportFilesDto
{
    public List<ParsedRowDto> HeaderRows  { get; set; } = [];
    public List<ParsedRowDto> AddressRows { get; set; } = [];
    public List<ParsedRowDto> ContactRows { get; set; } = [];
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

## Phase 5 — BPAssembler, Mapper, and Validator

### Step 5.1 — Implement the BPAssembler (Application layer)

The assembler takes the 3 parsed file results and groups them by `CardCode` into complete `BusinessPartner` domain objects with their child collections.

**`src/ServiceLayer.DTW.Application/Assembly/BPAssembler.cs`**
```csharp
using ServiceLayer.DTW.Application.DTOs;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Domain.Models;

namespace ServiceLayer.DTW.Application.Assembly;

public static class BPAssembler
{
    /// <summary>
    /// Assembles BusinessPartner domain objects from 3 separately parsed files.
    /// Returns one BusinessPartner per unique CardCode found in headerRows.
    /// Address and contact rows are matched by CardCode and attached as child collections.
    /// </summary>
    public static List<BusinessPartner> Assemble(BPImportFilesDto files)
    {
        // Index address and contact rows by CardCode for fast lookup
        var addressLookup = files.AddressRows
            .GroupBy(r => r.Fields.GetValueOrDefault("CardCode", string.Empty),
                     StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var contactLookup = files.ContactRows
            .GroupBy(r => r.Fields.GetValueOrDefault("CardCode", string.Empty),
                     StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var result = new List<BusinessPartner>();

        foreach (var row in files.HeaderRows)
        {
            var f        = row.Fields;
            var cardCode = f.GetValueOrDefault("CardCode", string.Empty);

            var bp = new BusinessPartner
            {
                CardCode        = cardCode,
                CardName        = f.GetValueOrDefault("CardName",        string.Empty),
                CardType        = ParseCardType(f.GetValueOrDefault("CardType", "C")),
                GroupCode       = ParseNullableInt(f.GetValueOrDefault("GroupCode")),
                Currency        = NullIfEmpty(f.GetValueOrDefault("Currency")),
                PayTermsGrpCode = ParseNullableInt(f.GetValueOrDefault("PayTermsGrpCode")),
                Phone1          = NullIfEmpty(f.GetValueOrDefault("Phone1")),
                EmailAddress    = NullIfEmpty(f.GetValueOrDefault("EmailAddress")),
                Website         = NullIfEmpty(f.GetValueOrDefault("Website")),
                FederalTaxID    = NullIfEmpty(f.GetValueOrDefault("FederalTaxID")),

                // UDF columns (U_*) stored separately and passed to mapper
                UdfFields = ExtractUdfs(f)
            };

            // Attach addresses for this CardCode
            if (addressLookup.TryGetValue(cardCode, out var addrRows))
                bp.Addresses = addrRows.Select(MapAddress).ToList();

            // Attach contacts for this CardCode
            if (contactLookup.TryGetValue(cardCode, out var contRows))
                bp.Contacts = contRows.Select(MapContact).ToList();

            result.Add(bp);
        }

        return result;
    }

    private static BPAddress MapAddress(ParsedRowDto row)
    {
        var f = row.Fields;
        return new BPAddress
        {
            AddressName = f.GetValueOrDefault("AddressName", string.Empty),
            AddressType = f.GetValueOrDefault("AddressType", string.Empty),
            Street      = f.GetValueOrDefault("Street",      string.Empty),
            City        = f.GetValueOrDefault("City",        string.Empty),
            ZipCode     = f.GetValueOrDefault("ZipCode",     string.Empty),
            Country     = f.GetValueOrDefault("Country",     string.Empty),
            State       = f.GetValueOrDefault("State",       string.Empty),
            UdfFields   = ExtractUdfs(f)
        };
    }

    private static ContactPerson MapContact(ParsedRowDto row)
    {
        var f = row.Fields;
        return new ContactPerson
        {
            Name        = f.GetValueOrDefault("Name",        string.Empty),
            FirstName   = f.GetValueOrDefault("FirstName",   string.Empty),
            LastName    = f.GetValueOrDefault("LastName",    string.Empty),
            Phone1      = f.GetValueOrDefault("Phone1",      string.Empty),
            MobilePhone = f.GetValueOrDefault("MobilePhone", string.Empty),
            Email       = f.GetValueOrDefault("E_Mail",      string.Empty),
            Position    = f.GetValueOrDefault("Position",    string.Empty),
            UdfFields   = ExtractUdfs(f)
        };
    }

    /// <summary>
    /// Extracts all U_* columns from a row's fields dictionary.
    /// These are passed to the SL mapper and serialized via [JsonExtensionData].
    /// </summary>
    private static Dictionary<string, string> ExtractUdfs(Dictionary<string, string> fields) =>
        fields
            .Where(kvp => kvp.Key.StartsWith("U_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

    private static CardType ParseCardType(string value) => value.ToUpperInvariant() switch
    {
        "C" or "CUSTOMER" => CardType.Customer,
        "S" or "SUPPLIER" => CardType.Supplier,
        "L" or "LEAD"     => CardType.Lead,
        _                  => CardType.Customer
    };

    private static int?    ParseNullableInt(string? v) => int.TryParse(v, out var r) ? r : null;
    private static string? NullIfEmpty(string? v)      => string.IsNullOrWhiteSpace(v) ? null : v;
}
```

> **Note:** `UdfFields` must be added to the domain models (`BusinessPartner`, `BPAddress`, `ContactPerson`). Update Step 2.2 accordingly.

### Step 5.1a — Add UdfFields to domain models

Add the following property to each of the three domain models:

```csharp
/// <summary>User-defined field values (U_* columns). Passed through to Service Layer.</summary>
public Dictionary<string, string> UdfFields { get; set; } = [];
```

Add it to:
- `src/ServiceLayer.DTW.Domain/Models/BusinessPartner.cs`
- `src/ServiceLayer.DTW.Domain/Models/BPAddress.cs`
- `src/ServiceLayer.DTW.Domain/Models/ContactPerson.cs`

### Step 5.2 — Implement the Business Partner mapper (with UDF passthrough)

The mapper converts domain objects to SL DTOs. Known fields map to explicit properties; `UdfFields` are written into `AdditionalProperties` which is serialized by `[JsonExtensionData]`.

**`src/ServiceLayer.DTW.Infrastructure/Mapping/BusinessPartnerMapper.cs`**
```csharp
using System.Text.Json;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Domain.Models;
using ServiceLayer.DTW.Infrastructure.ServiceLayer.DTOs;

namespace ServiceLayer.DTW.Infrastructure.Mapping;

public static class BusinessPartnerMapper
{
    /// <summary>
    /// Maps a domain BusinessPartner (assembled from header + address + contact files)
    /// to the SL DTO that will be serialized and POSTed/PATCHed to Service Layer.
    /// UDF fields (U_*) are passed through via [JsonExtensionData].
    /// </summary>
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

        // UDFs from the header file
        AdditionalProperties = ToJsonElementDict(bp.UdfFields),

        BPAddresses = bp.Addresses.Select(a => new SLBPAddress
        {
            AddressName          = a.AddressName,
            AddressType          = a.AddressType,
            Street               = a.Street,
            City                 = a.City,
            ZipCode              = a.ZipCode,
            Country              = a.Country,
            State                = a.State,
            AdditionalProperties = ToJsonElementDict(a.UdfFields)  // UDFs from address file
        }).ToList(),

        ContactEmployees = bp.Contacts.Select(c => new SLContactEmployee
        {
            Name                 = c.Name,
            FirstName            = c.FirstName,
            LastName             = c.LastName,
            Phone1               = c.Phone1,
            MobilePhone          = c.MobilePhone,
            Email                = c.Email,
            Position             = c.Position,
            AdditionalProperties = ToJsonElementDict(c.UdfFields)  // UDFs from contacts file
        }).ToList()
    };

    /// <summary>
    /// Converts string UDF values to JsonElement so they can be serialized
    /// by System.Text.Json via [JsonExtensionData].
    /// </summary>
    private static Dictionary<string, JsonElement>? ToJsonElementDict(Dictionary<string, string> udfs)
    {
        if (udfs.Count == 0) return null;

        return udfs.ToDictionary(
            kvp => kvp.Key,
            kvp => JsonSerializer.SerializeToElement(kvp.Value));
    }

    private static string CardTypeToSLString(CardType ct) => ct switch
    {
        CardType.Customer => "cCustomer",
        CardType.Supplier => "cSupplier",
        CardType.Lead     => "cLead",
        _                  => "cCustomer"
    };
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

The command now accepts **3 optional streams** — one per template file. Only the header stream is required.

**`src/ServiceLayer.DTW.Application/UseCases/ImportBusinessPartners/ImportBusinessPartnersCommand.cs`**
```csharp
using MediatR;
using ServiceLayer.DTW.Domain.Enums;

namespace ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;

public record ImportBusinessPartnersCommand(
    Stream     HeaderStream,
    string     HeaderFileName,
    Stream?    AddressStream,
    string?    AddressFileName,
    Stream?    ContactStream,
    string?    ContactFileName,
    ImportMode Mode,
    bool       StopOnError
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

The handler now:
1. Parses all 3 streams independently
2. Passes the results to `BPAssembler` to get assembled `BusinessPartner` objects
3. Validates and imports each assembled BP

**`src/ServiceLayer.DTW.Application/UseCases/ImportBusinessPartners/ImportBusinessPartnersHandler.cs`**
```csharp
using FluentValidation;
using MediatR;
using ServiceLayer.DTW.Application.Assembly;
using ServiceLayer.DTW.Application.DTOs;
using ServiceLayer.DTW.Application.Interfaces;
using ServiceLayer.DTW.Domain.Enums;

namespace ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;

public class ImportBusinessPartnersHandler
    : IRequestHandler<ImportBusinessPartnersCommand, ImportBusinessPartnersResult>
{
    private readonly FileParserResolver                            _parserResolver;
    private readonly IServiceLayerClient                           _slClient;
    private readonly IValidator<Domain.Models.BusinessPartner>     _validator;

    public ImportBusinessPartnersHandler(
        FileParserResolver                              parserResolver,
        IServiceLayerClient                             slClient,
        IValidator<Domain.Models.BusinessPartner>       validator)
    {
        _parserResolver = parserResolver;
        _slClient       = slClient;
        _validator      = validator;
    }

    public async Task<ImportBusinessPartnersResult> Handle(
        ImportBusinessPartnersCommand command,
        CancellationToken             ct)
    {
        var result = new ImportBusinessPartnersResult();

        // 1. Parse each file using the appropriate parser (CSV/TXT/XLSX)
        var files = new BPImportFilesDto
        {
            HeaderRows = await _parserResolver
                .Resolve(command.HeaderFileName)
                .ParseAsync(command.HeaderStream, command.HeaderFileName, ct),

            AddressRows = command.AddressStream is not null
                ? await _parserResolver
                    .Resolve(command.AddressFileName!)
                    .ParseAsync(command.AddressStream, command.AddressFileName!, ct)
                : [],

            ContactRows = command.ContactStream is not null
                ? await _parserResolver
                    .Resolve(command.ContactFileName!)
                    .ParseAsync(command.ContactStream, command.ContactFileName!, ct)
                : []
        };

        // 2. Assemble: group rows by CardCode into BusinessPartner objects
        var businessPartners = BPAssembler.Assemble(files);
        result.TotalRows = businessPartners.Count;

        // 3. Login to Service Layer
        await _slClient.LoginAsync(ct);

        try
        {
            int rowNum = 1;
            foreach (var bp in businessPartners)
            {
                var rowResult = new ImportRowResult
                {
                    RowNumber = rowNum++,
                    CardCode  = bp.CardCode
                };
                result.RowResults.Add(rowResult);

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

The controller accepts **3 multipart files**: header (required), addresses (optional), contacts (optional).

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
    /// Upload DTW-style template files to import Business Partners.
    /// file_header is required. file_addresses and file_contacts are optional.
    /// Each file can be CSV, TXT, or XLSX.
    /// UDF columns (U_*) in any file are automatically passed to Service Layer.
    /// </summary>
    [HttpPost("business-partners")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportJobResponse>> ImportBusinessPartners(
        IFormFile            fileHeader,
        IFormFile?           fileAddresses = null,
        IFormFile?           fileContacts  = null,
        [FromForm] ImportMode mode         = ImportMode.Upsert,
        [FromForm] bool       stopOnError  = false,
        CancellationToken     ct           = default)
    {
        if (fileHeader is null || fileHeader.Length == 0)
            return BadRequest("Header file is required.");

        await using var headerStream  = fileHeader.OpenReadStream();
        await using var addressStream = fileAddresses?.OpenReadStream();
        await using var contactStream = fileContacts?.OpenReadStream();

        var command = new ImportBusinessPartnersCommand(
            HeaderStream:    headerStream,
            HeaderFileName:  fileHeader.FileName,
            AddressStream:   addressStream,
            AddressFileName: fileAddresses?.FileName,
            ContactStream:   contactStream,
            ContactFileName: fileContacts?.FileName,
            Mode:            mode,
            StopOnError:     stopOnError);

        var result = await _mediator.Send(command, ct);

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

    /// <summary>
    /// Sends up to 3 template files to the API.
    /// addressStream and contactStream are optional.
    /// </summary>
    public async Task<ImportJobResponse?> ImportBusinessPartnersAsync(
        Stream     headerStream,
        string     headerFileName,
        Stream?    addressStream,
        string?    addressFileName,
        Stream?    contactStream,
        string?    contactFileName,
        ImportMode mode,
        bool       stopOnError)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(headerStream), "fileHeader", headerFileName);

        if (addressStream is not null && addressFileName is not null)
            content.Add(new StreamContent(addressStream), "fileAddresses", addressFileName);

        if (contactStream is not null && contactFileName is not null)
            content.Add(new StreamContent(contactStream), "fileContacts", contactFileName);

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

The UI provides **three separate file pickers** — one per template type. Only the header file is required.

**`src/ServiceLayer.DTW.Web/Pages/Import.razor`**
```razor
@page "/import"
@using ServiceLayer.DTW.Domain.Enums
@using ServiceLayer.DTW.Shared.Responses
@using ServiceLayer.DTW.Web.Services
@inject ImportApiClient ApiClient

<h2>Import Business Partners</h2>

<table>
    <tr>
        <td><strong>Header file</strong> (required)</td>
        <td><InputFile OnChange="e => _fileHeader = e.File" accept=".csv,.txt,.xlsx" /></td>
        <td><small>BusinessPartners.csv — CardCode, CardName, CardType, Phone1, U_*</small></td>
    </tr>
    <tr>
        <td>Addresses file (optional)</td>
        <td><InputFile OnChange="e => _fileAddresses = e.File" accept=".csv,.txt,.xlsx" /></td>
        <td><small>BPAddresses.csv — CardCode, AddressType, Street, City, U_*</small></td>
    </tr>
    <tr>
        <td>Contacts file (optional)</td>
        <td><InputFile OnChange="e => _fileContacts = e.File" accept=".csv,.txt,.xlsx" /></td>
        <td><small>ContactEmployees.csv — CardCode, Name, FirstName, E_Mail, U_*</small></td>
    </tr>
</table>

<div style="margin-top:1rem">
    <label>Import Mode: </label>
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

<button @onclick="RunImport" disabled="@(_fileHeader is null || _importing)" style="margin-top:1rem">
    @(_importing ? "Importing..." : "Start Import")
</button>

@if (_result is not null)
{
    <hr />
    <h3>Results</h3>
    <p>
        Total: @_result.TotalRows |
        Success: @_result.SuccessCount |
        Errors: @_result.ErrorCount |
        Skipped: @_result.SkippedCount
    </p>

    <table>
        <thead>
            <tr><th>Row</th><th>CardCode</th><th>Status</th><th>Message</th></tr>
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
    private IBrowserFile? _fileHeader;
    private IBrowserFile? _fileAddresses;
    private IBrowserFile? _fileContacts;
    private ImportMode    _mode        = ImportMode.Upsert;
    private bool          _stopOnError = false;
    private bool          _importing   = false;
    private ImportJobResponse? _result;
    private string?       _error;

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    private async Task RunImport()
    {
        if (_fileHeader is null) return;
        _importing = true;
        _result    = null;
        _error     = null;

        try
        {
            await using var headerStream  = _fileHeader.OpenReadStream(MaxFileSize);
            await using var addressStream = _fileAddresses?.OpenReadStream(MaxFileSize);
            await using var contactStream = _fileContacts?.OpenReadStream(MaxFileSize);

            _result = await ApiClient.ImportBusinessPartnersAsync(
                headerStream,  _fileHeader.Name,
                addressStream, _fileAddresses?.Name,
                contactStream, _fileContacts?.Name,
                _mode, _stopOnError);
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

The handler now uses `FileParserResolver` and `BPAssembler`. Mock the resolver to return a mock parser.

**`tests/ServiceLayer.DTW.Application.Tests/ImportBusinessPartnersHandlerTests.cs`**
```csharp
using System.Text;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using ServiceLayer.DTW.Application.Assembly;
using ServiceLayer.DTW.Application.DTOs;
using ServiceLayer.DTW.Application.Interfaces;
using ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Domain.Models;
using ServiceLayer.DTW.Infrastructure.Parsing;

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
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<string>(), default))
            .ReturnsAsync(new List<ParsedRowDto>
            {
                new() { RowNumber = 1, Fields = new(StringComparer.OrdinalIgnoreCase) {
                    { "CardCode", "C001" }, { "CardName", "Acme" }, { "CardType", "C" }
                }}
            });

        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<BusinessPartner>(), default))
            .ReturnsAsync(new ValidationResult());

        slClientMock
            .Setup(s => s.BusinessPartnerExistsAsync("C001", default))
            .ReturnsAsync(false);

        // Resolver returns the mock parser for any file name
        var resolver = new FileParserResolver(new[] { parserMock.Object });

        var handler = new ImportBusinessPartnersHandler(
            resolver, slClientMock.Object, validatorMock.Object);

        var command = new ImportBusinessPartnersCommand(
            HeaderStream:    new MemoryStream(),
            HeaderFileName:  "test.csv",
            AddressStream:   null,
            AddressFileName: null,
            ContactStream:   null,
            ContactFileName: null,
            Mode:            ImportMode.Upsert,
            StopOnError:     false);

        var result = await handler.Handle(command, default);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Should_Extract_Udf_Fields_From_Header_Row()
    {
        // Verifies that U_* columns are captured by the assembler
        var rows = new List<ParsedRowDto>
        {
            new() { RowNumber = 1, Fields = new(StringComparer.OrdinalIgnoreCase) {
                { "CardCode", "C001" }, { "CardName", "Acme" }, { "CardType", "C" },
                { "U_TaxRegion", "Northeast" }, { "U_CustomerTier", "Gold" }
            }}
        };

        var files  = new BPImportFilesDto { HeaderRows = rows };
        var result = BPAssembler.Assemble(files);

        Assert.Single(result);
        Assert.Equal("Northeast", result[0].UdfFields["U_TaxRegion"]);
        Assert.Equal("Gold",      result[0].UdfFields["U_CustomerTier"]);
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

### Step 11.3 — Sample import files

**`sample-BusinessPartners.csv`** (header file — required)
```
CardCode,CardName,CardType,Phone1,EmailAddress,FederalTaxID,U_TaxRegion,U_CustomerTier
C001,Acme Corporation,C,+1-555-0100,info@acme.com,12-3456789,Northeast,Gold
S001,Big Supplier Ltd,S,+1-555-0200,orders@bigsupplier.com,98-7654321,West,Standard
C002,New Lead Co,L,,leads@newlead.com,,,
```

**`sample-BPAddresses.csv`** (address file — optional, linked by CardCode)
```
CardCode,AddressName,AddressType,Street,City,ZipCode,Country,U_Region
C001,Bill To,bo_BillTo,123 Main St,New York,10001,US,NY
C001,Ship To,bo_ShipTo,456 Warehouse Ave,Brooklyn,11201,US,NY
S001,Bill To,bo_BillTo,789 Supply Rd,Los Angeles,90001,US,CA
```

**`sample-ContactEmployees.csv`** (contacts file — optional, linked by CardCode)
```
CardCode,Name,FirstName,LastName,Phone1,MobilePhone,E_Mail,Position
C001,John Smith,John,Smith,+1-555-0101,+1-555-9901,john@acme.com,Accounts Payable
S001,Jane Doe,Jane,Doe,+1-555-0201,,jane@bigsupplier.com,Sales Rep
```

Upload these 3 files via the Scalar UI (`/scalar/v1`) or the Blazor UI to verify end-to-end import.

---

## Reference — Template Column Names

### Business Partner Header Template (`BusinessPartners.csv`)

| Column Name      | Required | Notes                                              |
|------------------|----------|----------------------------------------------------|  
| `CardCode`       | Yes      | Max 15 chars. Primary key linking all 3 files.     |
| `CardName`       | Yes      | Max 100 chars                                      |
| `CardType`       | Yes      | `C` = Customer, `S` = Supplier, `L` = Lead         |
| `GroupCode`      | No       | Integer                                            |
| `Currency`       | No       | e.g., `USD`, `EUR`                                 |
| `PayTermsGrpCode`| No       | Integer                                            |
| `Phone1`         | No       |                                                    |
| `EmailAddress`   | No       | Validated email format                             |
| `Website`        | No       |                                                    |
| `FederalTaxID`   | No       |                                                    |
| `U_*`            | No       | Any UDF column — passed through automatically      |

### Address Template (`BPAddresses.csv`)

| Column Name   | Required | Notes                                              |
|---------------|----------|----------------------------------------------------|  
| `CardCode`    | Yes      | Must match a row in the header file                |
| `AddressName` | Yes      | e.g., `Bill To`, `Main Warehouse`                  |
| `AddressType` | Yes      | `bo_BillTo` or `bo_ShipTo`                        |
| `Street`      | No       |                                                    |
| `City`        | No       |                                                    |
| `ZipCode`     | No       |                                                    |
| `Country`     | No       | Two-letter ISO code (e.g., `US`, `DE`)             |
| `State`       | No       |                                                    |
| `U_*`         | No       | Any UDF column — passed through automatically      |

### Contacts Template (`ContactEmployees.csv`)

| Column Name   | Required | Notes                                              |
|---------------|----------|----------------------------------------------------|  
| `CardCode`    | Yes      | Must match a row in the header file                |
| `Name`        | Yes      | Full display name                                  |
| `FirstName`   | No       |                                                    |
| `LastName`    | No       |                                                    |
| `Phone1`      | No       |                                                    |
| `MobilePhone` | No       |                                                    |
| `E_Mail`      | No       |                                                    |
| `Position`    | No       |                                                    |
| `U_*`         | No       | Any UDF column — passed through automatically      |

---

## Common Issues and Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| SSL error connecting to SL | Self-signed cert on SAP server | Use `DangerousAcceptAnyServerCertificateValidator` in dev; use a valid cert in production |
| `401 Unauthorized` from SL | Session expired or login failed | Ensure cookie container is shared in `HttpClientHandler`; call `LoginAsync` before each batch |
| `400 Bad Request` from SL | Missing required field or wrong value | Check SL error message in `error.message.value`; review CardType string (`cCustomer` not `Customer`) |
| UDF column not sent to SL | Column name doesn't start with `U_` | Prefix the column name with `U_` exactly (case-insensitive) |
| Address rows not linked | `CardCode` in address file doesn't match header file | Check for extra spaces or case differences; parser uses `OrdinalIgnoreCase` |
| Large file upload rejected | Blazor default max file size is 512KB | `MaxFileSize` constant in `Import.razor` is set to 10MB; increase if needed |
| CORS error in browser | API not allowing Blazor origin | Configure `WithOrigins` in `AddCors` in `Program.cs` |
