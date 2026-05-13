using AutoMapper;
using Restaurant.Application.Features.Catalog.ProductTypes;
using Restaurant.Application.Features.Crm.Customers;
using Restaurant.Application.Features.Operations.DiningTables;
using Restaurant.Application.Features.Organization.Employees;
using Restaurant.Application.Features.Procurement.Providers;
using Restaurant.Application.Features.Reservations;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Mapping;

public sealed class ProductTypeMappingProfile : Profile
{
    public ProductTypeMappingProfile() => CreateMap<ProductType, ProductTypeDto>();
}

public sealed class ProviderMappingProfile : Profile
{
    public ProviderMappingProfile() => CreateMap<Provider, ProviderDto>();
}

public sealed class EmployeeMappingProfile : Profile
{
    public EmployeeMappingProfile() => CreateMap<Employee, EmployeeDto>();
}

public sealed class DiningTableMappingProfile : Profile
{
    public DiningTableMappingProfile() => CreateMap<DiningTable, DiningTableDto>();
}

public sealed class CustomerMappingProfile : Profile
{
    public CustomerMappingProfile() => CreateMap<Customer, CustomerDto>();
}

public sealed class ReservationMappingProfile : Profile
{
    public ReservationMappingProfile() => CreateMap<Reservation, ReservationDto>();
}
