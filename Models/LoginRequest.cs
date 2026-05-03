using System.ComponentModel.DataAnnotations;

namespace SecureAPIGateway.Models;

/// <summary>
/// Credentials sent by the client to the login endpoint.
/// </summary>
public class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
