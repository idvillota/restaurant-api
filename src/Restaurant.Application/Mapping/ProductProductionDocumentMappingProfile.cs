using AutoMapper;
using Restaurant.Application.Features.Inventory.ProductProductions;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Mapping;

public sealed class ProductProductionDocumentMappingProfile : Profile
{
    public ProductProductionDocumentMappingProfile()
    {
        CreateMap<ProductProductionDocument, ProductProductionDocumentListItemDto>()
            .ForMember(d => d.LineCount, o => o.MapFrom(s => s.Lines.Count))
            .ForMember(d => d.CreatedByUserEmail, o => o.MapFrom(s => s.CreatedByUser.Email));

        CreateMap<ProductProductionDocument, ProductProductionDocumentDto>()
            .IncludeBase<ProductProductionDocument, ProductProductionDocumentListItemDto>();

        CreateMap<ProductProductionLine, ProductProductionLineDto>()
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.Product.Name));
    }
}
