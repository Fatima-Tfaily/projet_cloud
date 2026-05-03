using Serilog;

namespace SecureAPIGateway.Middleware;

/// <summary>
/// Logs every incoming request and its final response status code.
/// This runs first in the pipeline so it captures ALL events — including blocked ones.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip     = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var method = context.Request.Method;
        var path   = context.Request.Path;
        var query  = context.Request.QueryString.Value ?? string.Empty;
        var start  = DateTime.UtcNow;

        // Let the rest of the pipeline handle the request
        await _next(context);

        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
        var status  = context.Response.StatusCode;

        // Choose log level based on status code
        if (status >= 400)
        {
            Log.Warning("[Gateway] {IP} {Method} {Path}{Query} → {Status} ({Elapsed}ms)",
                ip, method, path, query, status, elapsed);
        }
        else
        {
            Log.Information("[Gateway] {IP} {Method} {Path}{Query} → {Status} ({Elapsed}ms)",
                ip, method, path, query, status, elapsed);
        }
    }
}
