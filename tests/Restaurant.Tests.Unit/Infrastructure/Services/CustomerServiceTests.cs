using Restaurant.Application.Features.Crm.Customers;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class CustomerServiceTests
{
    private static CustomerService CreateSut(TenantDbFixture fx) =>
        new(
            fx.Repository<Restaurant.Domain.Entities.Customer>(),
            fx.Repository<Restaurant.Domain.Entities.Reservation>(),
            fx.UnitOfWork,
            fx.Mapper);

    [Fact]
    public async Task CreateAsync_persists_month_and_day_without_year()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);

        var created = await sut.CreateAsync(
            new CreateCustomerDto
            {
                Name = "Ana Martínez",
                Email = "ana@example.com",
                BirthMonth = 5,
                BirthDay = 14,
            });

        Assert.Equal(5, created.BirthMonth);
        Assert.Equal(14, created.BirthDay);

        var loaded = await sut.GetByIdAsync(created.Id);
        Assert.Equal(5, loaded!.BirthMonth);
        Assert.Equal(14, loaded.BirthDay);
    }

    [Fact]
    public async Task CreateAsync_allows_29_february()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);

        var created = await sut.CreateAsync(
            new CreateCustomerDto { Name = "Leap", BirthMonth = 2, BirthDay = 29 });

        Assert.Equal(2, created.BirthMonth);
        Assert.Equal(29, created.BirthDay);
    }

    [Fact]
    public async Task CreateAsync_rejects_invalid_month_day()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(new CreateCustomerDto { Name = "X", BirthMonth = 2, BirthDay = 30 }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(new CreateCustomerDto { Name = "X", BirthMonth = 5, BirthDay = null }));
    }

    [Fact]
    public async Task UpdateAsync_can_clear_birthday()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            new CreateCustomerDto { Name = "Juan Pérez", BirthMonth = 2, BirthDay = 1 });

        var updated = await sut.UpdateAsync(
            created.Id,
            new UpdateCustomerDto { Name = created.Name, BirthMonth = null, BirthDay = null, IsActive = true });

        Assert.Null(updated!.BirthMonth);
        Assert.Null(updated.BirthDay);
    }
}
