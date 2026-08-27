namespace Restaurant.Application.Common.Options;

/// <summary>
/// Configures where generated documents (kitchen tickets, sales receipts) are stored.
/// Local for development, Azure Blob Storage for cloud environments.
/// </summary>
public sealed class GeneratedFileStorageOptions
{
    public const string SectionName = "GeneratedFiles";

    /// <summary>"Local" (default) or "AzureBlob".</summary>
    public string Provider { get; set; } = "Local";

    public LocalGeneratedFileStorageOptions Local { get; set; } = new();

    public AzureBlobGeneratedFileStorageOptions AzureBlob { get; set; } = new();
}

public sealed class LocalGeneratedFileStorageOptions
{
    /// <summary>Base folder relative to the API content root (or an absolute path).</summary>
    public string RootPath { get; set; } = "files";
}

public sealed class AzureBlobGeneratedFileStorageOptions
{
    /// <summary>Full connection string. Preferred for local/dev testing against a storage account.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Storage account name. Used with managed identity when no connection string is set.</summary>
    public string? AccountName { get; set; }

    /// <summary>Container that holds the generated files.</summary>
    public string ContainerName { get; set; } = "generated-files";

    /// <summary>When true (and no connection string), authenticate via DefaultAzureCredential.</summary>
    public bool UseManagedIdentity { get; set; }
}
