using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.UserPreferences;

public sealed class UpdateUserPreferencesDto
{
    [Required]
    [MaxLength(32)]
    public string BrandTheme { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string ColorScheme { get; set; } = string.Empty;
}
