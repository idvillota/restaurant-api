namespace Restaurant.Application.Common.Options;

public sealed class ProductImageOptions
{
    public const string SectionName = "ProductImages";

    /// <summary>Physical root directory (e.g. uploads/products).</summary>
    public string RootPath { get; set; } = "uploads/products";

    /// <summary>Public URL prefix (e.g. /media/products).</summary>
    public string PublicBasePath { get; set; } = "/media/products";

    public long MaxBytes { get; set; } = 5 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
}
