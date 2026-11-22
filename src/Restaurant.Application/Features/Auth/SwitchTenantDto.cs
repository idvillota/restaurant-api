using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Auth;

public sealed class SwitchTenantDto
{
    [Required]
    [MaxLength(100)]
    public string TenantSlug { get; set; } = string.Empty;
}
