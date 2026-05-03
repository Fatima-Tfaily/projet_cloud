namespace SecureAPIGateway.Services;

/// <summary>
/// Contract for generating JWT bearer tokens.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates a signed JWT string for the given username and role.
    /// </summary>
    string GenerateToken(string username, string role = "User");
}
