using AutoMapper;
using Restaurant.Application.Features.Procurement.Purchases;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Mapping;

public sealed class PurchaseMappingProfile : Profile
{
    public PurchaseMappingProfile()
    {
        CreateMap<Purchase, PurchaseDto>()
            .ForMember(d => d.ProviderName, opt => opt.MapFrom(s => s.Provider.Name))
            .ForMember(d => d.Lines, opt => opt.MapFrom(s => s.Lines));

        CreateMap<PurchaseLine, PurchaseLineDto>()
            .ForMember(d => d.IngredientName, opt => opt.MapFrom(s => s.Ingredient.Name))
            .ForMember(d => d.IngredientUnit, opt => opt.MapFrom(s => s.Ingredient.Unit));

        CreateMap<Purchase, PurchaseListItemDto>()
            .ForMember(d => d.ProviderName, opt => opt.MapFrom(s => s.Provider.Name))
            .ForMember(d => d.LineCount, opt => opt.MapFrom(s => s.Lines.Count));
    }
}
