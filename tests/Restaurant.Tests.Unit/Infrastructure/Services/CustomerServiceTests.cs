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

    [Fact]
    public async Task CreateAsync_normalizes_tax_id_and_rejects_duplicates()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);

        var created = await sut.CreateAsync(
            new CreateCustomerDto { Name = "Ana", TaxId = "1.234.567", Address = "Cra 1" });
        Assert.Equal("1234567", created.TaxId);
        Assert.Equal("Cra 1", created.Address);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(new CreateCustomerDto { Name = "Otra", TaxId = "1234567" }));
    }

    [Fact]
    public async Task FindByTaxIdAsync_matches_normalized_digits()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);
        await sut.CreateAsync(new CreateCustomerDto { Name = "Ana", TaxId = "1234567", Phone = "300" });

        var found = await sut.FindByTaxIdAsync("1.234.567");
        Assert.NotNull(found);
        Assert.Equal("Ana", found!.Name);
    }

    [Fact]
    public async Task UpsertDeliveryProfileAsync_skips_address_when_not_updating_profile()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);
        await sut.UpsertDeliveryProfileAsync(new StartDeliveryCustomerDto
        {
            TaxId = "555",
            Name = "Luis",
            Phone = "301",
            Address = "Calle 1",
        });

        var second = await sut.UpsertDeliveryProfileAsync(new StartDeliveryCustomerDto
        {
            TaxId = "555",
            Name = "Luis",
            Phone = "302",
            Address = "Casa de mamá",
            UpdateProfileAddress = false,
        });

        Assert.Equal("Calle 1", second.Address);
        Assert.Equal("302", second.Phone);
        Assert.Equal("Calle 1", (await fx.Db.Customers.SingleAsync()).Address);
    }

    [Fact]
    public async Task UpsertDeliveryProfileAsync_rejects_consumidor_final()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpsertDeliveryProfileAsync(new StartDeliveryCustomerDto
            {
                TaxId = "2222222",
                Name = "X",
                Phone = "300",
                Address = "Cra 1",
            }));
    }
}
