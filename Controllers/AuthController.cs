using Microsoft.AspNetCore.Mvc;
using SecureAPIGateway.Models;
using SecureAPIGateway.Services;

namespace SecureAPIGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IJwtService jwtService, ILogger<AuthController> logger)
    {
        _jwtService = jwtService;
        _logger     = logger;
    }

    /// <summary>
    /// Authenticates a user and returns a signed JWT token.
    /// POST /api/auth/login
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // ── Validate the model (checks [Required] attributes) ────────────────
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // ── Verify credentials ───────────────────────────────────────────────
        // NOTE: In a real system, query the database here.
        // This is a hardcoded example for demonstration.
        var (isValid, role) = ValidateCredentials(request.Username, request.Password);

        if (!isValid)
        {
            _logger.LogWarning("[Auth] Failed login attempt for user: {Username}", request.Username);

            // Use 401, NOT 403. 401 = "who are you?", 403 = "I know who you are, but no."
            return Unauthorized(new { error = "Invalid username or password" });
        }

        // ── Generate and return the token ────────────────────────────────────
        var token = _jwtService.GenerateToken(request.Username, role);

        _logger.LogInformation("[Auth] Token issued for user: {Username} (Role: {Role})",
            request.Username, role);

        return Ok(new
        {
            token     = token,
            expiresIn = 3600,   // seconds — tell the client when to refresh
            tokenType = "Bearer"
        });
    }

    // ── Private helper — replace with DB lookup in production ────────────────
    private static (bool IsValid, string Role) ValidateCredentials(string username, string password)
    {
        // Hardcoded demo users — swap this for a real UserService + hashed passwords
        var users = new Dictionary<string, (string Password, string Role)>
        {
            { "admin",   ("Admin@123",  "Admin") },
            { "gateway", ("Gateway@1",  "User")  },
        };

        if (users.TryGetValue(username, out var entry) && entry.Password == password)
            return (true, entry.Role);

        return (false, string.Empty);
    }
}
