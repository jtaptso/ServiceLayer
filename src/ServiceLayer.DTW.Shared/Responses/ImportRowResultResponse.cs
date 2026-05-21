using ServiceLayer.DTW.Domain.Enums;

namespace ServiceLayer.DTW.Shared.Responses;

public class ImportRowResultResponse
{
    public int             RowNumber { get; set; }
    public string          CardCode  { get; set; } = string.Empty;
    public ImportRowStatus Status    { get; set; }
    public string?         Message   { get; set; }
}