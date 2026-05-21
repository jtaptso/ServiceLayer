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