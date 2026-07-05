namespace Restaurant.Application.Features.Auth;

public sealed class MultipleTenantsLoginException : Exception
{
    public const string ErrorCode = "multiple_tenants";

    public MultipleTenantsLoginException(IReadOnlyList<LoginTenantOptionDto> tenants)
        : base("Su cuenta está en varios locales. Elija con cuál desea ingresar.")
    {
        Tenants = tenants;
    }

    public IReadOnlyList<LoginTenantOptionDto> Tenants { get; }
}
