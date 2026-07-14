using Restaurant.Application.Features.Platform;

namespace Restaurant.Application.Common.Interfaces;

public interface ITenantInitialDataImportService
{
    /// <summary>Builds a sample .xlsx template with required sheets and header rows.</summary>
    byte[] BuildTemplate();

    /// <summary>
    /// Validates the workbook and, if valid, creates the tenant and initial data in one transaction.
    /// Throws <see cref="TenantInitialDataValidationException"/> when validation fails.
    /// </summary>
    Task<TenantInitialDataImportResultDto> ImportAsync(Stream excelStream, CancellationToken cancellationToken = default);
}
