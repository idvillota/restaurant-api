namespace Restaurant.Application.Common.Options;

public sealed class ProductImageOptions
{
    public const string SectionName = "ProductImages";

    /// <summary>"Local" (default) or "AzureBlob".</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>
    /// Physical root directory for uploads. Stored image paths are scoped as
    /// <c>products/{tenant}/{product}.ext</c> underneath it. Used by the local provider.
    /// </summary>
    public string RootPath { get; set; } = "uploads";

    /// <summary>Public URL prefix served by the API (e.g. /media). Used by the local provider.</summary>
    public string PublicBasePath { get; set; } = "/media";

    public long MaxBytes { get; set; } = 5 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    public ProductImageAzureBlobOptions AzureBlob { get; set; } = new();
}

public sealed class ProductImageAzureBlobOptions
{
    /// <summary>Full connection string. Preferred for local/dev testing against a storage account.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Storage account name. Used with managed identity when no connection string is set.</summary>
    public string? AccountName { get; set; }

    /// <summary>Container that holds product images. Should allow anonymous blob read.</summary>
    public string ContainerName { get; set; } = "media";

    /// <summary>When true (and no connection string), authenticate via DefaultAzureCredential.</summary>
    public bool UseManagedIdentity { get; set; }

    /// <summary>
    /// Optional explicit public base URL (e.g. a CDN endpoint). When empty, the container URL is used.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
