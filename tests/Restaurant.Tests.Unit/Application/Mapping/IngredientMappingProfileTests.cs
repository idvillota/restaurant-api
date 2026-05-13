using AutoMapper;
using Restaurant.Application.Features.Catalog.Ingredients;
using Restaurant.Application.Mapping;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;

namespace Restaurant.Tests.Unit.Application.Mapping;

public sealed class IngredientMappingProfileTests
{
    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<IngredientMappingProfile>()).CreateMapper();

    [Fact]
    public void AutoMapper_configuration_is_valid()
    {
        var cfg = new MapperConfiguration(cfg => cfg.AddProfile<IngredientMappingProfile>());
        cfg.AssertConfigurationIsValid();
    }

    [Fact]
    public void Maps_ingredient_to_dto()
    {
        var mapper = CreateMapper();
        var entity = new Ingredient
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Olive oil",
            Unit = IngredientUnit.Liter,
            StockQuantity = 2.5m,
            ReorderLevel = 1m,
            IsActive = true,
        };

        var dto = mapper.Map<IngredientDto>(entity);

        Assert.Equal(entity.Id, dto.Id);
        Assert.Equal(entity.Name, dto.Name);
        Assert.Equal(entity.Unit, dto.Unit);
        Assert.Equal(entity.StockQuantity, dto.StockQuantity);
        Assert.Equal(entity.ReorderLevel, dto.ReorderLevel);
        Assert.True(dto.IsActive);
    }
}
