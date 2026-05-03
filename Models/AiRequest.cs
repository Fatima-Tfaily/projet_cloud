namespace SecureAPIGateway.Models;

/// <summary>
/// Data sent to the AI Detection Engine for classification.
/// </summary>
public class AiRequest
{
    public string IpAddress { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string QueryString { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string BodySnippet { get; set; } = string.Empty;  // First 500 chars of body
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
