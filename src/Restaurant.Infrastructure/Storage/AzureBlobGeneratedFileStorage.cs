using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;

namespace Restaurant.Infrastructure.Storage;

/// <summary>Stores generated documents in an Azure Blob Storage container.</summary>
public sealed class AzureBlobGeneratedFileStorage : IGeneratedFileStorage
{
    private readonly AzureBlobGeneratedFileStorageOptions _blob;
    private readonly object _gate = new();
    private BlobContainerClient? _container;
    private bool _containerEnsured;

    public AzureBlobGeneratedFileStorage(IOptions<GeneratedFileStorageOptions> options)
    {
        _blob = options.Value.AzureBlob;
        // Lazy-create the client on first use so unrelated endpoints (e.g. table list) still work
        // if storage credentials are missing or invalid until a PDF is actually saved.
        if (string.IsNullOrWhiteSpace(_blob.ConnectionString)
            && !(_blob.UseManagedIdentity && !string.IsNullOrWhiteSpace(_blob.AccountName)))
        {
            throw new InvalidOperationException(
                "GeneratedFiles:AzureBlob requires ConnectionString, or UseManagedIdentity=true with AccountName. " +
                "For fully local runs set GeneratedFiles:Provider=Local.");
        }
    }

    public async Task<string> SaveAsync(
        string relativePath,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var blobName = NormalizeBlobName(relativePath);
        var container = GetContainer();
        await EnsureContainerAsync(container, cancellationToken);

        var blobClient = container.GetBlobClient(blobName);
        using var stream = new MemoryStream(content, writable: false);
        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);

        return blobName;
    }

    public async Task<GeneratedFileContent?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var container = GetContainer();
        var blobClient = container.GetBlobClient(NormalizeBlobName(relativePath));
        if (!await blobClient.ExistsAsync(cancellationToken))
            return null;

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        var contentType = response.Value.Details.ContentType;
        if (string.IsNullOrWhiteSpace(contentType))
            contentType = "application/octet-stream";

        return new GeneratedFileContent(response.Value.Content, contentType);
    }

    private BlobContainerClient GetContainer()
    {
        if (_container is not null)
            return _container;

        lock (_gate)
        {
            if (_container is not null)
                return _container;

            BlobServiceClient serviceClient;
            if (!string.IsNullOrWhiteSpace(_blob.ConnectionString))
            {
                try
                {
                    serviceClient = new BlobServiceClient(_blob.ConnectionString);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "GeneratedFiles:AzureBlob ConnectionString is invalid. " +
                        "Replace AccountKey=YOUR_KEY with the real storage account key " +
                        "(az storage account show-connection-string -g restaurant-dev-rg -n restaurantdevstore).",
                        ex);
                }
            }
            else
            {
                var endpoint = new Uri($"https://{_blob.AccountName}.blob.core.windows.net");
                serviceClient = new BlobServiceClient(endpoint, new DefaultAzureCredential());
            }

            _container = serviceClient.GetBlobContainerClient(_blob.ContainerName);
            return _container;
        }
    }

    private async Task EnsureContainerAsync(BlobContainerClient container, CancellationToken cancellationToken)
    {
        if (_containerEnsured)
            return;

        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _containerEnsured = true;
    }

    private static string NormalizeBlobName(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');
}
