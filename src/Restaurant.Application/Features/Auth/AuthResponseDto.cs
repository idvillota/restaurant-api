namespace Restaurant.Application.Features.Auth;

public sealed class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string TenantSlug { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    public string BrandTheme { get; set; } = string.Empty;
    public string ColorScheme { get; set; } = string.Empty;
}
