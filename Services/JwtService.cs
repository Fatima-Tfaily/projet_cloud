using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SecureAPIGateway.Services;

/// <summary>
/// Creates signed JWT tokens using settings from appsettings.json.
/// </summary>
public class JwtService : IJwtService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public JwtService(IConfiguration configuration)
    {
        _secret       = configuration["JwtSettings:SecretKey"]!;
        _issuer       = configuration["JwtSettings:Issuer"]!;
        _audience     = configuration["JwtSettings:Audience"]!;
        _expiryMinutes = configuration.GetValue<int>("JwtSettings:ExpiryMinutes", 60);
    }

    public string GenerateToken(string username, string role = "User")
    {
        // ── 1. Signing credentials ────────────────────────────────────────────
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // ── 2. Claims (payload data embedded in the token) ───────────────────
        // Claims are key-value pairs the server can read without hitting the DB.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),       // subject (who)
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // unique token ID
            new Claim(JwtRegisteredClaimNames.Iat,                  // issued at
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
        };

        // ── 3. Build the token ───────────────────────────────────────────────
        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds
        );

        // ── 4. Serialize to the eyJhbGci... string ───────────────────────────
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
