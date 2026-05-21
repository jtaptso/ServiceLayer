namespace ServiceLayer.DTW.Shared.Responses;

public class ImportJobResponse
{
    public int                          TotalRows    { get; set; }
    public int                          SuccessCount { get; set; }
    public int                          ErrorCount   { get; set; }
    public int                          SkippedCount { get; set; }
    public List<ImportRowResultResponse> RowResults  { get; set; } = [];
}