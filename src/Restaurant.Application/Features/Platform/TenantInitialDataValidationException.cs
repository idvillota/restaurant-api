using Restaurant.Application.Features.Platform;

namespace Restaurant.Application.Features.Platform;

public sealed class TenantInitialDataValidationException : Exception
{
    public TenantInitialDataValidationException(IReadOnlyList<TenantInitialDataErrorDto> errors)
        : base("La carga inicial contiene errores de validación.")
    {
        Errors = errors;
    }

    public IReadOnlyList<TenantInitialDataErrorDto> Errors { get; }
}
