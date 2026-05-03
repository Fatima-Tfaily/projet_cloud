using SecureAPIGateway.Models;

namespace SecureAPIGateway.Services;

/// <summary>
/// Contract for the AI Detection Engine integration.
/// </summary>
public interface IAiDetectionService
{
    /// <summary>
    /// Sends request metadata to the AI service and returns its classification result.
    /// </summary>
    Task<AiResponse> AnalyzeAsync(AiRequest request);
}
