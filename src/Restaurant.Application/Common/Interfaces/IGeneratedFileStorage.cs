namespace Restaurant.Application.Common.Interfaces;

/// <summary>
/// Storage abstraction for generated documents (kitchen tickets, sales receipts).
/// The <paramref name="relativePath"/> is a forward-slash logical path that includes the
/// scope prefix (e.g. <c>orders/{tenant}/{file}.pdf</c> or <c>receipts/{tenant}/{file}.xml</c>).
/// Implementations map it to a local folder or a blob name.
/// </summary>
public interface IGeneratedFileStorage
{
    Task<string> SaveAsync(
        string relativePath,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<GeneratedFileContent?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}

/// <summary>Content stream plus content type for a stored file. Caller disposes the stream.</summary>
public sealed record GeneratedFileContent(Stream Stream, string ContentType);
