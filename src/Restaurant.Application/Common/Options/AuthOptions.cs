namespace Restaurant.Application.Common.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>When false, POST /api/Auth/register is rejected. Leave true on local/dev only.</summary>
    public bool AllowPublicRegistration { get; set; }

    public int LoginPermitLimit { get; set; } = 20;

    public int LoginWindowMinutes { get; set; } = 1;

    public int RegisterPermitLimit { get; set; } = 5;

    public int RegisterWindowMinutes { get; set; } = 10;
}
