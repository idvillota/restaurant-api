using AutoMapper;
using Restaurant.Application.Features.Sales.SalesOrders;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Mapping;

public sealed class SalesOrderMappingProfile : Profile
{
    public SalesOrderMappingProfile()
    {
        CreateMap<SalesOrder, SalesOrderDto>()
            .ForMember(d => d.DiningTableCode, o => o.MapFrom(s => s.DiningTable != null ? s.DiningTable.Code : null));

        CreateMap<SalesOrderLine, SalesOrderLineDto>()
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.Product.Name))
            .ForMember(d => d.CompositionType, o => o.MapFrom(s => s.Product.CompositionType));

        CreateMap<SalesOrderLineExcludedIngredient, SalesOrderLineExcludedIngredientDto>()
            .ForMember(d => d.IngredientName, o => o.MapFrom(s => s.Ingredient.Name));
    }
}
