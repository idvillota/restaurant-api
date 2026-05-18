using AutoMapper;
using Restaurant.Application.Mapping;

namespace Restaurant.Tests.Unit.Application.Mapping;

public sealed class CrudEntityMappingProfilesTests
{
    [Fact]
    public void All_crud_profiles_are_valid()
    {
        var cfg = new MapperConfiguration(c =>
        {
            c.AddProfile<ProductTypeMappingProfile>();
            c.AddProfile<ProviderMappingProfile>();
            c.AddProfile<PurchaseMappingProfile>();
            c.AddProfile<EmployeeMappingProfile>();
            c.AddProfile<DiningTableMappingProfile>();
            c.AddProfile<CustomerMappingProfile>();
            c.AddProfile<ReservationMappingProfile>();
        });
        cfg.AssertConfigurationIsValid();
    }
}
