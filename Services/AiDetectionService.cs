using System.Net.Http.Json;
using SecureAPIGateway.Models;
using Serilog;

namespace SecureAPIGateway.Services;

/// <summary>
/// Sends request metadata to the Python AI Detection Engine via HTTP
/// and returns its maliciousness classification.
/// </summary>
public class AiDetectionService : IAiDetectionService
{
    private readonly HttpClient _httpClient;
    private readonly string _analyzeEndpoint;

    public AiDetectionService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient      = httpClient;
        _analyzeEndpoint = configuration["AiServiceSettings:AnalyzeEndpoint"] ?? "/api/analyze";
    }

    public async Task<AiResponse> AnalyzeAsync(AiRequest request)
    {
        try
        {
            // POST the metadata to the AI Flask service
            var response = await _httpClient.PostAsJsonAsync(_analyzeEndpoint, request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AiResponse>();
                return result ?? new AiResponse { IsMalicious = false };
            }

            // AI returned an error — log and fail-open
            Log.Warning("[AiDetection] AI service returned {Status}. Failing open.", response.StatusCode);
            return new AiResponse { IsMalicious = false };
        }
        catch (HttpRequestException ex)
        {
            // AI service is down — log and fail-open to avoid full outage
            Log.Error(ex, "[AiDetection] Could not reach AI service. Failing open.");
            return new AiResponse { IsMalicious = false };
        }
        catch (TaskCanceledException)
        {
            // Timeout
            Log.Warning("[AiDetection] AI service timed out. Failing open.");
            return new AiResponse { IsMalicious = false };
        }
    }
}
