using System.Net.Http.Json;
using CentralisationService.Entities.Models.Vision;
using Microsoft.Extensions.Options;
using CentralServer.Models;

namespace CentralServer.Services;

public sealed class NeuroRetailAnalysisService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RetailDetectionMonitoringOptions _options;

    public NeuroRetailAnalysisService(
        IHttpClientFactory httpClientFactory,
        IOptions<RetailDetectionMonitoringOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<RetailSceneAnalysisResponse> AnalyzeAsync(
        RetailSceneAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(NeuroRetailAnalysisService));
        client.BaseAddress = new Uri(_options.NeuroBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.NeuroTimeoutSeconds));

        using var response = await client.PostAsJsonAsync("api/analysis/retail-scene", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RetailSceneAnalysisResponse>(cancellationToken);
        return payload ?? new RetailSceneAnalysisResponse
        {
            Note = "Neuro returned an empty response payload.",
        };
    }
}
