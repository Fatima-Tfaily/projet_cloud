using System.Text.RegularExpressions;
using SecureAPIGateway.Models;
using Serilog;

namespace SecureAPIGateway.Middleware;

/// <summary>
/// Inspects query strings and request bodies for SQL Injection and XSS patterns.
/// Returns 400 Bad Request if a threat is detected.
/// </summary>
public class InputValidationMiddleware
{
    private readonly RequestDelegate _next;

    // SQL Injection patterns (case-insensitive)
    private static readonly Regex SqlInjectionPattern = new(
        @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|EXEC|UNION|TRUNCATE|CAST|CONVERT)\b)|(--)|(;)|(\/\*)|(\*\/)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // XSS patterns
    private static readonly Regex XssPattern = new(
        @"(<script[\s\S]*?>[\s\S]*?<\/script>)|(<[^>]+\s+on\w+\s*=)|(javascript\s*:)|(vbscript\s*:)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public InputValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // ── 1. Check query string ────────────────────────────────────────────
        var queryString = context.Request.QueryString.Value ?? string.Empty;

        if (IsMalicious(queryString, out var queryReason))
        {
            await BlockRequest(context, queryReason);
            return;
        }

        // ── 2. Check request body (for POST/PUT/PATCH) ───────────────────────
        if (context.Request.ContentLength > 0 &&
            (context.Request.Method == HttpMethods.Post ||
             context.Request.Method == HttpMethods.Put  ||
             context.Request.Method == HttpMethods.Patch))
        {
            // Enable buffering so we can read the body AND the controller can read it again
            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // rewind for the next middleware/controller

            if (IsMalicious(body, out var bodyReason))
            {
                await BlockRequest(context, bodyReason);
                return;
            }
        }

        await _next(context);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsMalicious(string input, out string reason)
    {
        reason = string.Empty;

        if (SqlInjectionPattern.IsMatch(input))
        {
            reason = "SQL Injection pattern detected";
            return true;
        }

        if (XssPattern.IsMatch(input))
        {
            reason = "XSS pattern detected";
            return true;
        }

        return false;
    }

    private static async Task BlockRequest(HttpContext context, string reason)
    {
        Log.Warning("[InputValidation] BLOCKED {Method} {Path} from {IP} — Reason: {Reason}",
            context.Request.Method,
            context.Request.Path,
            context.Connection.RemoteIpAddress,
            reason);

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            $"{{\"error\":\"Request blocked\",\"reason\":\"{reason}\"}}");
    }
}
