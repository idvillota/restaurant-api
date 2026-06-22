using AutoMapper;
using Restaurant.Application.Features.Inventory.IngredientMovements;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Mapping;

public sealed class IngredientMovementDocumentMappingProfile : Profile
{
    public IngredientMovementDocumentMappingProfile()
    {
        CreateMap<IngredientMovementDocument, IngredientMovementDocumentListItemDto>()
            .ForMember(d => d.MovementTypeName, o => o.MapFrom(s => s.MovementType.Name))
            .ForMember(d => d.IsInput, o => o.MapFrom(s => s.MovementType.IsInput))
            .ForMember(d => d.LineCount, o => o.MapFrom(s => s.Lines.Count))
            .ForMember(d => d.CreatedByUserEmail, o => o.MapFrom(s => s.CreatedByUser.Email));

        CreateMap<IngredientMovementDocument, IngredientMovementDocumentDto>()
            .IncludeBase<IngredientMovementDocument, IngredientMovementDocumentListItemDto>();

        CreateMap<IngredientMovement, IngredientMovementLineDto>()
            .ForMember(d => d.IngredientName, o => o.MapFrom(s => s.Ingredient.Name));
    }
}
