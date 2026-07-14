namespace Restaurant.Application.Common.Options;

public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    /// <summary>Emails allowed to call platform endpoints (e.g. tenant Excel import).</summary>
    public string[] AdminEmails { get; set; } = [];

    /// <summary>Default password for standard starter users created on import.</summary>
    public string StarterUserPassword { get; set; } = "Demo123!";
}
