using System.Threading;

namespace Restaurant.Application.Common.Interfaces;

public enum PrintFileType
{
    Factura,
    Comanda
}

/// <summary>
/// Servicio que centraliza la resolución y apertura de documentos generados (XML/PDF).
/// Devuelve null si no se encuentra el documento.
/// </summary>
public interface IPrintService
{
    Task<GeneratedFileContent?> GetDocumentAsync(
        Guid id,
        PrintFileType type,
        string? overridePath,
        CancellationToken cancellationToken = default);
}
