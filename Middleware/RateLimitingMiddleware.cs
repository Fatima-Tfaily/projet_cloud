using System.Collections.Concurrent;
using Serilog;

namespace SecureAPIGateway.Middleware;

/// <summary>
/// Limits each unique IP address to a configurable number of requests per time window.
/// Returns 429 Too Many Requests when the limit is exceeded.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _maxRequests;
    private readonly TimeSpan _window;

    // Thread-safe dictionary: IP → (request count, window start time)
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)>
        _requestCounts = new();

    public RateLimitingMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _maxRequests = configuration.GetValue<int>("RateLimitSettings:MaxRequestsPerWindow", 100);
        var windowSeconds = configuration.GetValue<int>("RateLimitSettings:WindowSeconds", 60);
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;

        _requestCounts.AddOrUpdate(
            key: ip,
            addValue: (1, now),
            updateValueFactory: (_, existing) =>
            {
                // If the time window has expired, start a fresh window
                if (now - existing.WindowStart > _window)
                    return (1, now);

                return (existing.Count + 1, existing.WindowStart);
            });

        var current = _requestCounts[ip];

        if (current.Count > _maxRequests)
        {
            var retryAfter = (int)(_window - (now - current.WindowStart)).TotalSeconds;

            Log.Warning("[RateLimit] BLOCKED IP {IP} — {Count}/{Max} requests in window",
                ip, current.Count, _maxRequests);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = retryAfter.ToString();
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $"{{\"error\":\"Rate limit exceeded\",\"retryAfterSeconds\":{retryAfter}}}");
            return;
        }

        await _next(context);
    }
}
