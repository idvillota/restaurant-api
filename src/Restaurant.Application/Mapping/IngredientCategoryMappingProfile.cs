using AutoMapper;
using Restaurant.Application.Features.Catalog.IngredientCategories;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Mapping;

public sealed class IngredientCategoryMappingProfile : Profile
{
    public IngredientCategoryMappingProfile() => CreateMap<IngredientCategory, IngredientCategoryDto>();
}
