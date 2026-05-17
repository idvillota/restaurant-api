using AutoMapper;
using Restaurant.Application.Features.Catalog;
using Restaurant.Application.Mapping;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;

namespace Restaurant.Tests.Unit.Application.Mapping;

public sealed class ProductMappingProfileTests
{
    [Fact]
    public void AutoMapper_configuration_is_valid()
    {
        var cfg = new MapperConfiguration(cfg => cfg.AddProfile<ProductMappingProfile>());
        cfg.AssertConfigurationIsValid();
    }

    [Fact]
    public void Maps_product_to_list_item_including_type_name()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ProductMappingProfile>()).CreateMapper();
        var type = new ProductType { Id = Guid.NewGuid(), Name = "Beverages", TenantId = Guid.NewGuid() };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductTypeId = type.Id,
            ProductType = type,
            Name = "Cola",
            Description = "Sparkling cola drink",
            Sku = "BEV-001",
            UnitPrice = 3.50m,
            CompositionType = EProductType.Resale,
            IsActive = true,
        };

        var dto = mapper.Map<ProductListItemDto>(product);

        Assert.Equal(product.Id, dto.Id);
        Assert.Equal(EProductType.Resale, dto.CompositionType);
        Assert.Equal("Cola", dto.Name);
        Assert.Equal("Sparkling cola drink", dto.Description);
        Assert.Equal("BEV-001", dto.Sku);
        Assert.Equal(3.50m, dto.UnitPrice);
        Assert.Equal(type.Id, dto.ProductTypeId);
        Assert.Equal("Beverages", dto.ProductTypeName);
    }
}
