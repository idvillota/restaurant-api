namespace Restaurant.Application.Features.Auth;
using System.ComponentModel.DataAnnotations;

public sealed class RegisterTenantDto
{
    public string TenantName { get; set; } = string.Empty;
    [Required]
    public string AdminEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? AdminDisplayName { get; set; }
}
