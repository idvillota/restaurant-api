using AutoMapper;
using Restaurant.Application.Features.Catalog.Ingredients;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Mapping;

public sealed class IngredientMappingProfile : Profile
{
    public IngredientMappingProfile()
    {
        CreateMap<Ingredient, IngredientDto>();
    }
}
