using AutoMapper;
using Restaurant.Application.Features.Catalog;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Mapping;

public sealed class ProductMappingProfile : Profile
{
    public ProductMappingProfile()
    {
        CreateMap<Product, ProductListItemDto>()
            .ForMember(d => d.ProductTypeName, o => o.MapFrom(s => s.ProductType.Name))
            .ForMember(d => d.CostPrice, o => o.Ignore())
            .ForMember(d => d.ImageUrl, o => o.Ignore());
    }
}
