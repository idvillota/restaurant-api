using AutoMapper;
using Restaurant.Application.Features.Inventory.IngredientMovementTypes;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Mapping;

public sealed class IngredientMovementTypeMappingProfile : Profile
{
    public IngredientMovementTypeMappingProfile() =>
        CreateMap<IngredientMovementType, IngredientMovementTypeDto>();
}
