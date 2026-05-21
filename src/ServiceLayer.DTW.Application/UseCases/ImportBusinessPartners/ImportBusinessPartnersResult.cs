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