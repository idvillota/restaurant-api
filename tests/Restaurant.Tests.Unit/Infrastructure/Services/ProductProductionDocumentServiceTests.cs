using AutoMapper;
using Moq;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Inventory.ProductProductions;
using Restaurant.Application.Mapping;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class ProductProductionDocumentServiceTests
{
    [Fact]
    public async Task CreateAsync_adds_stock_additively()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        fx.Db.TenantSettings.Add(
            new TenantSettings
            {
                TenantId = fx.TenantId,
                InventoryEnabled = false,
                StockControlMode = StockControlMode.ProductUnits,
            });
        fx.Db.Users.Add(
            new User
            {
                Id = userId,
                Email = "chef@test.local",
                PasswordHash = "x",
            });
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Sancocho",
                CompositionType = EProductType.Prepared,
                UnitPrice = 18m,
                StockQuantity = 10m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var tenant = new Mock<ICurrentTenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns(fx.TenantId);
        tenant.SetupGet(t => t.UserId).Returns(userId);

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ProductProductionDocumentMappingProfile>())
            .CreateMapper();
        var uow = new UnitOfWork(fx.Db);
        var sut = new ProductProductionDocumentService(
            new Repository<ProductProductionDocument>(fx.Db),
            new Repository<ProductProductionLine>(fx.Db),
            new Repository<Product>(fx.Db),
            tenant.Object,
            uow,
            mapper,
            fx.Db);

        await sut.CreateAsync(
            new CreateProductProductionDocumentDto
            {
                DocumentNumber = "PROD-1",
                Lines = [new CreateProductProductionLineDto { ProductId = productId, Quantity = 20m }],
            });

        await sut.CreateAsync(
            new CreateProductProductionDocumentDto
            {
                DocumentNumber = "PROD-2",
                Lines = [new CreateProductProductionLineDto { ProductId = productId, Quantity = 5m }],
            });

        var product = await fx.Db.Products.FindAsync(productId);
        Assert.NotNull(product);
        Assert.Equal(35m, product!.StockQuantity);

        var docs = fx.Db.ProductProductionDocuments.ToList();
        Assert.Equal(2, docs.Count);
    }

    [Fact]
    public async Task CreateAsync_assigns_PROD_number_when_document_number_blank()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        fx.Db.TenantSettings.Add(
            new TenantSettings
            {
                TenantId = fx.TenantId,
                InventoryEnabled = false,
                StockControlMode = StockControlMode.ProductUnits,
            });
        fx.Db.Users.Add(
            new User
            {
                Id = userId,
                Email = "chef@test.local",
                PasswordHash = "x",
            });
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Arepa",
                CompositionType = EProductType.Prepared,
                UnitPrice = 5m,
                StockQuantity = 0m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var tenant = new Mock<ICurrentTenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns(fx.TenantId);
        tenant.SetupGet(t => t.UserId).Returns(userId);

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ProductProductionDocumentMappingProfile>())
            .CreateMapper();
        var sut = new ProductProductionDocumentService(
            new Repository<ProductProductionDocument>(fx.Db),
            new Repository<ProductProductionLine>(fx.Db),
            new Repository<Product>(fx.Db),
            tenant.Object,
            new UnitOfWork(fx.Db),
            mapper,
            fx.Db);

        var suggested = await sut.SuggestNextDocumentNumberAsync();
        Assert.StartsWith("PROD-", suggested);
        Assert.Matches(@"^PROD-\d{8}-\d{4}$", suggested);

        var created = await sut.CreateAsync(
            new CreateProductProductionDocumentDto
            {
                DocumentNumber = "  ",
                Lines = [new CreateProductProductionLineDto { ProductId = productId, Quantity = 3m }],
            });

        Assert.Matches(@"^PROD-\d{8}-\d{4}$", created.DocumentNumber);

        var second = await sut.CreateAsync(
            new CreateProductProductionDocumentDto
            {
                DocumentNumber = "",
                Lines = [new CreateProductProductionLineDto { ProductId = productId, Quantity = 1m }],
            });

        Assert.NotEqual(created.DocumentNumber, second.DocumentNumber);
        Assert.Matches(@"^PROD-\d{8}-\d{4}$", second.DocumentNumber);
    }
}
