using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;

namespace Restaurant.Infrastructure.Storage;

/// <summary>
/// Stores product images in an Azure Blob Storage container. The container must allow anonymous
/// blob read (or be fronted by a CDN via <see cref="ProductImageAzureBlobOptions.PublicBaseUrl"/>)
/// so the SPA can render images directly by URL.
/// </summary>
public sealed class AzureBlobProductImageStorage : IProductImageStorage
{
    private readonly ProductImageOptions _options;
    private readonly BlobContainerClient _container;
    private readonly string _publicBaseUrl;
    private bool _containerEnsured;

    public AzureBlobProductImageStorage(IOptions<ProductImageOptions> options)
    {
        _options = options.Value;
        var blob = _options.AzureBlob;

        BlobServiceClient serviceClient;
        if (!string.IsNullOrWhiteSpace(blob.ConnectionString))
        {
            serviceClient = new BlobServiceClient(blob.ConnectionString);
        }
        else if (blob.UseManagedIdentity && !string.IsNullOrWhiteSpace(blob.AccountName))
        {
            var endpoint = new Uri($"https://{blob.AccountName}.blob.core.windows.net");
            serviceClient = new BlobServiceClient(endpoint, new DefaultAzureCredential());
        }
        else
        {
            throw new InvalidOperationException(
                "ProductImages:AzureBlob requires ConnectionString, or UseManagedIdentity=true with AccountName. " +
                "For fully local runs set ProductImages:Provider=Local.");
        }

        _container = serviceClient.GetBlobContainerClient(blob.ContainerName);
        _publicBaseUrl = string.IsNullOrWhiteSpace(blob.PublicBaseUrl)
            ? _container.Uri.ToString().TrimEnd('/')
            : blob.PublicBaseUrl.TrimEnd('/');
    }

    public async Task<string> SaveAsync(
        Guid tenantId,
        Guid productId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var extension = NormalizeExtension(Path.GetExtension(fileName));
        if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"File type '{extension}' is not allowed.");

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > _options.MaxBytes)
            throw new InvalidOperationException($"Image must be {_options.MaxBytes / (1024 * 1024)} MB or smaller.");
        buffer.Position = 0;

        var relativePath = $"products/{tenantId:N}/{productId:N}{extension}";
        await EnsureContainerAsync(cancellationToken);

        var blobClient = _container.GetBlobClient(relativePath);
        await blobClient.UploadAsync(
            buffer,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = GetContentType(extension) } },
            cancellationToken);

        return relativePath;
    }

    public async Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;

        var blobClient = _container.GetBlobClient(NormalizeBlobName(relativePath));
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public string? GetPublicUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        return $"{_publicBaseUrl}/{NormalizeBlobName(relativePath)}";
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerEnsured)
            return;

        await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        _containerEnsured = true;
    }

    private static string NormalizeBlobName(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ".jpg";

        extension = extension.Trim().ToLowerInvariant();
        return extension.StartsWith('.') ? extension : $".{extension}";
    }

    private static string GetContentType(string extension) =>
        extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };
}
