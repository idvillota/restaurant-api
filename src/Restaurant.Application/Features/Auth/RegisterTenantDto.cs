namespace Restaurant.Application.Features.Auth;

public sealed class RegisterTenantDto
{
    public string TenantName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? AdminDisplayName { get; set; }
}
