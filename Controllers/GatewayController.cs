using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureAPIGateway.Models;
using SecureAPIGateway.Services;

namespace SecureAPIGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GatewayController : ControllerBase
{
    private readonly IAiDetectionService _aiDetection;
    private readonly ILogger<GatewayController> _logger;

    public GatewayController(IAiDetectionService aiDetection, ILogger<GatewayController> logger)
    {
        _aiDetection = aiDetection;
        _logger      = logger;
    }

    /// <summary>
    /// Main gateway entry point. Requires a valid JWT token.
    /// Runs AI detection on the request metadata before forwarding.
    /// </summary>
    [HttpGet]
    [HttpPost]
    [HttpPut]
    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> HandleRequest()
    {
        // Build the AI request from the current HTTP context
        var aiRequest = new AiRequest
        {
            IpAddress  = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            HttpMethod = Request.Method,
            Path       = Request.Path,
            QueryString = Request.QueryString.Value ?? string.Empty,
            Timestamp  = DateTime.UtcNow,
            Headers    = Request.Headers
                .Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key, h => h.Value.ToString()),
        };

        // Call AI Detection Engine
        var aiResult = await _aiDetection.AnalyzeAsync(aiRequest);

        if (aiResult.IsMalicious)
        {
            _logger.LogWarning(
                "[Gateway] AI BLOCKED request from {IP}: ThreatType={Threat}, Confidence={Score:P0}",
                aiRequest.IpAddress, aiResult.ThreatType, aiResult.ConfidenceScore);

            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error      = "Request blocked by AI detection",
                threatType = aiResult.ThreatType,
                confidence = aiResult.ConfidenceScore
            });
        }

        // ── Request is clean — forward metadata to backend (placeholder) ────
        _logger.LogInformation(
            "[Gateway] Request from {IP} PASSED all checks. Ready to forward.",
            aiRequest.IpAddress);

        return Ok(new
        {
            message   = "Request accepted by Secure API Gateway",
            ip        = aiRequest.IpAddress,
            method    = aiRequest.HttpMethod,
            path      = aiRequest.Path,
            timestamp = aiRequest.Timestamp
        });
    }
}
