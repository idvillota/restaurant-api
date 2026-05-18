using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Infrastructure.Persistence.Seeding;

internal static class DevelopmentSeedAssets
{
    /// <summary>
    /// One image per <see cref="DevelopmentSeedIds.ProductIds"/> index (same order as catalog seed).
    /// </summary>
    private static readonly string[] ProductImageFiles =
    [
        "pizza-margarita.jpg",
        "pasta-penne.jpg",
        "ensalada-cesar.jpg",
        "hamburguesa.jpg",
        "tiramisu.jpg",
        "cola.jpg",
        "agua-gas.jpg",
        "pan-ajo.jpg",
        "sopa.jpg",
        "pizza-pepperoni.jpg",
        "pollo-parrilla.jpg",
        "limonada.jpg",
    ];

    public static string? ResolveProductImagesDirectory(IHostEnvironment environment)
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "seed-data", "product-images"),
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "seed-data", "product-images")),
        };

        return candidates.Select(Path.GetFullPath).FirstOrDefault(Directory.Exists);
    }

    public static async Task ApplyProductImagesAsync(
        ApplicationDbContext db,
        Guid tenantId,
        IHostEnvironment environment,
        IProductImageStorage imageStorage,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var imagesDir = ResolveProductImagesDirectory(environment);
        if (imagesDir is null)
        {
            logger.LogWarning("Seed product images skipped: folder seed-data/product-images was not found.");
            return;
        }

        var applied = 0;
        for (var i = 0; i < DevelopmentSeedIds.ProductIds.Length; i++)
        {
            var productId = DevelopmentSeedIds.ProductIds[i];
            var product = await db.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken);
            if (product is null)
                continue;

            var fileName = ProductImageFiles[i];
            var filePath = Path.Combine(imagesDir, fileName);
            if (!File.Exists(filePath))
            {
                logger.LogWarning("Seed image not found for product index {Index}: {Path}", i, filePath);
                continue;
            }

            await using var stream = File.OpenRead(filePath);
            product.ImagePath = await imageStorage.SaveAsync(
                tenantId,
                product.Id,
                stream,
                fileName,
                cancellationToken);
            applied++;
        }

        logger.LogInformation("Applied {Count} product images from seed assets.", applied);
    }
}
