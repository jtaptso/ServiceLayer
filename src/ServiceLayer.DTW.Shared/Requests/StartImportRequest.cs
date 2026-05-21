using ServiceLayer.DTW.Domain.Enums;

namespace ServiceLayer.DTW.Shared.Requests;

public class StartImportRequest
{
    public ImportMode Mode        { get; set; } = ImportMode.Upsert;
    public bool       StopOnError { get; set; } = false;
}