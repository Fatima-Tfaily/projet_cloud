namespace SecureAPIGateway.Models;

/// <summary>
/// Represents a single logged HTTP request event.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string IpAddress { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string QueryString { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public bool WasBlocked { get; set; }
    public string BlockedReason { get; set; } = string.Empty;
}
