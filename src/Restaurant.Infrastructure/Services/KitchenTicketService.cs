using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Application.Features.Sales.KitchenTickets;
using Restaurant.Application.Features.Sales.SalesOrders;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.KitchenTickets;

namespace Restaurant.Infrastructure.Services;

public sealed class KitchenTicketService : IKitchenTicketService
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<Ingredient> _ingredients;
    private readonly IRepository<ProductIngredient> _productIngredients;
    private readonly IRepository<ProductBundleLine> _productBundleLines;
    private readonly IRepository<User> _users;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly string _absoluteRoot;

    static KitchenTicketService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public KitchenTicketService(
        IRepository<Product> products,
        IRepository<Ingredient> ingredients,
        IRepository<ProductIngredient> productIngredients,
        IRepository<ProductBundleLine> productBundleLines,
        IRepository<User> users,
        ICurrentTenantContext tenantContext,
        IOptions<KitchenTicketOptions> options,
        IHostEnvironment environment)
    {
        _products = products;
        _ingredients = ingredients;
        _productIngredients = productIngredients;
        _productBundleLines = productBundleLines;
        _users = users;
        _tenantContext = tenantContext;

        var rootPath = options.Value.RootPath.Trim().TrimEnd('/', '\\');
        _absoluteRoot = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.Combine(environment.ContentRootPath, rootPath);
        Directory.CreateDirectory(_absoluteRoot);
    }

    public async Task<KitchenTicketModel> BuildTicketModelAsync(
        SalesOrder order,
        IReadOnlyList<AddSalesOrderLineDto> batchLines,
        CancellationToken cancellationToken = default)
    {
        var sentBy = await ResolveSentByNameAsync(cancellationToken);

        var productIds = batchLines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _products.Query()
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var bundleByProduct = await LoadBundleLinesAsync(productIds, cancellationToken);
        var componentProductIds = bundleByProduct.Values
            .SelectMany(lines => lines.Select(l => l.ComponentProductId))
            .Distinct()
            .Where(id => !products.ContainsKey(id))
            .ToList();

        if (componentProductIds.Count > 0)
        {
            var components = await _products.Query()
                .AsNoTracking()
                .Where(p => componentProductIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var (id, product) in components)
                products[id] = product;
        }

        var excludedIds = batchLines
            .SelectMany(l => l.ExcludedIngredientIds)
            .Distinct()
            .ToList();

        var ingredientNameById = excludedIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _ingredients.Query()
                .AsNoTracking()
                .Where(i => excludedIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.Name, cancellationToken);

        var recipeByProduct = await LoadRecipeIngredientIdsAsync(productIds, cancellationToken);

        var lines = new List<KitchenTicketLineModel>();
        foreach (var lineDto in batchLines)
        {
            if (!products.TryGetValue(lineDto.ProductId, out var product))
                throw new InvalidOperationException("Product was not found or is inactive.");

            if (product.CompositionType == EProductType.Bundle)
            {
                AppendBundleKitchenLines(
                    lines,
                    product,
                    lineDto,
                    bundleByProduct.GetValueOrDefault(product.Id) ?? [],
                    products);
                continue;
            }

            var excludedNames = ResolveExcludedNames(
                product.CompositionType,
                lineDto.ExcludedIngredientIds,
                recipeByProduct.GetValueOrDefault(product.Id) ?? [],
                ingredientNameById);

            lines.Add(
                new KitchenTicketLineModel
                {
                    ProductName = product.Name,
                    Quantity = lineDto.Quantity,
                    Notes = NormalizeNotes(lineDto.Notes),
                    ExcludedIngredientNames = excludedNames,
                });
        }

        return new KitchenTicketModel
        {
            TableCode = order.DiningTable?.Code ?? "—",
            OrderNumber = order.Number,
            SentBy = sentBy,
            SentAtUtc = DateTime.UtcNow,
            Lines = lines,
        };
    }

    public Task<string?> GeneratePdfAsync(KitchenTicketModel model, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (model.Lines.Count == 0)
            return Task.FromResult<string?>(null);

        var tenantFolder = _tenantContext.TenantId?.ToString("N") ?? "shared";
        var tenantRoot = Path.Combine(_absoluteRoot, tenantFolder);
        Directory.CreateDirectory(tenantRoot);

        var safeTable = SanitizeFileToken(model.TableCode);
        var fileName =
            $"kitchen_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safeTable}_{Guid.NewGuid():N}.pdf";
        var absolutePath = Path.Combine(tenantRoot, fileName);

        var pdfBytes = QuestPdfKitchenTicketDocument.BuildPdf(model);
        File.WriteAllBytes(absolutePath, pdfBytes);

        var relativePath = Path.Combine(tenantFolder, fileName).Replace('\\', '/');
        return Task.FromResult<string?>(relativePath);
    }

    private async Task<string> ResolveSentByNameAsync(CancellationToken cancellationToken)
    {
        if (_tenantContext.UserId is not { } userId)
            return "—";

        var user = await _users.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return "—";

        return string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName.Trim();
    }

    private async Task<Dictionary<Guid, List<(Guid ComponentProductId, decimal Quantity)>>> LoadBundleLinesAsync(
        List<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return [];

        var rows = await _productBundleLines.Query()
            .AsNoTracking()
            .Where(bl => productIds.Contains(bl.ProductId))
            .OrderBy(bl => bl.SortOrder)
            .Select(bl => new { bl.ProductId, bl.ComponentProductId, bl.Quantity })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => (x.ComponentProductId, x.Quantity)).ToList());
    }

    private static void AppendBundleKitchenLines(
        List<KitchenTicketLineModel> lines,
        Product bundleProduct,
        AddSalesOrderLineDto lineDto,
        IReadOnlyList<(Guid ComponentProductId, decimal Quantity)> bundleLines,
        IReadOnlyDictionary<Guid, Product> products)
    {
        var notes = NormalizeNotes(lineDto.Notes);
        var hasKitchenWork = false;

        foreach (var (componentProductId, componentQuantity) in bundleLines)
        {
            if (!products.TryGetValue(componentProductId, out var component))
                continue;

            if (component.CompositionType != EProductType.Prepared)
                continue;

            hasKitchenWork = true;
            lines.Add(
                new KitchenTicketLineModel
                {
                    ProductName = $"{bundleProduct.Name} · {component.Name}",
                    Quantity = lineDto.Quantity * componentQuantity,
                    Notes = notes,
                    ExcludedIngredientNames = [],
                });
        }

        if (!hasKitchenWork)
        {
            lines.Add(
                new KitchenTicketLineModel
                {
                    ProductName = bundleProduct.Name,
                    Quantity = lineDto.Quantity,
                    Notes = notes,
                    ExcludedIngredientNames = [],
                });
        }
    }

    private async Task<Dictionary<Guid, HashSet<Guid>>> LoadRecipeIngredientIdsAsync(
        List<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return [];

        var rows = await _productIngredients.Query()
            .AsNoTracking()
            .Where(pi => productIds.Contains(pi.ProductId))
            .Select(pi => new { pi.ProductId, pi.IngredientId })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.IngredientId).ToHashSet());
    }

    private static IReadOnlyList<string> ResolveExcludedNames(
        EProductType compositionType,
        List<Guid> requested,
        HashSet<Guid> recipeIngredientIds,
        Dictionary<Guid, string> ingredientNameById)
    {
        if (compositionType != EProductType.Prepared || requested.Count == 0)
            return [];

        var distinct = requested.Distinct().OrderBy(id => id).ToList();
        if (distinct.Any(id => !recipeIngredientIds.Contains(id)))
            throw new InvalidOperationException("Excluded ingredients must belong to the product recipe.");

        return distinct
            .Select(id => ingredientNameById.TryGetValue(id, out var name) ? name : id.ToString())
            .ToList();
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;
        return notes.Trim();
    }

    private static string SanitizeFileToken(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return "mesa";

        var chars = trimmed
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();
        return new string(chars);
    }
}
