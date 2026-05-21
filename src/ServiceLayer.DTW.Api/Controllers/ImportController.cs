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
    /// Upload a flat wide CSV, TXT, or XLSX file to import Business Partners.
    /// One row = one Business Partner. Addresses and contacts are embedded as
    /// prefixed columns (BillTo_*, ShipTo_*, Contact1_*, …).
    /// U_* columns are automatically passed to Service Layer as UDFs.
    /// </summary>
    [HttpPost("business-partners")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportJobResponse>> ImportBusinessPartners(
        IFormFile            file,
        [FromForm] ImportMode mode        = ImportMode.Upsert,
        [FromForm] bool       stopOnError = false,
        CancellationToken     ct          = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        await using var stream = file.OpenReadStream();

        var command = new ImportBusinessPartnersCommand(
            FileStream:  stream,
            FileName:    file.FileName,
            Mode:        mode,
            StopOnError: stopOnError);

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
}