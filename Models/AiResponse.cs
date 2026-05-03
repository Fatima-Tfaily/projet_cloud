namespace SecureAPIGateway.Models;

/// <summary>
/// Response received from the AI Detection Engine.
/// </summary>
public class AiResponse
{
    public bool IsMalicious { get; set; }
    public double ConfidenceScore { get; set; }  // 0.0 - 1.0
    public string ThreatType { get; set; } = string.Empty;  // e.g. "SQLi", "DDoS", "Normal"
}
