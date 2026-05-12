namespace Restaurant.Application.Features.Auth;

public sealed class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? TenantSlug { get; set; }
}
