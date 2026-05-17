using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Procurement.Providers;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class ProviderServiceTests
{
    [Fact]
    public async Task CreateAsync_persists_contact_and_address()
    {
        using var fx = new TenantDbFixture();
        var sut = new ProviderService(fx.Repository<Provider>(), fx.UnitOfWork, fx.Mapper);

        var created = await sut.CreateAsync(
            new CreateProviderDto
            {
                Name = "Fresh Foods Co.",
                ContactName = "Maria Lopez",
                Address = "123 Market St, Suite 4",
                Email = "orders@freshfoods.example",
            });

        Assert.Equal("Maria Lopez", created.ContactName);
        Assert.Equal("123 Market St, Suite 4", created.Address);

        var stored = await fx.Db.Providers.SingleAsync(p => p.Id == created.Id);
        Assert.Equal("Maria Lopez", stored.ContactName);
        Assert.Equal("123 Market St, Suite 4", stored.Address);
    }

    [Fact]
    public async Task UpdateAsync_updates_contact_and_address()
    {
        using var fx = new TenantDbFixture();
        var id = Guid.NewGuid();
        fx.Db.Providers.Add(
            new Provider
            {
                Id = id,
                TenantId = fx.TenantId,
                Name = "Legacy Supplier",
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new ProviderService(fx.Repository<Provider>(), fx.UnitOfWork, fx.Mapper);
        var updated = await sut.UpdateAsync(
            id,
            new UpdateProviderDto
            {
                Name = "Legacy Supplier",
                ContactName = "Alex Kim",
                Address = "9 Industrial Blvd",
                IsActive = true,
            });

        Assert.NotNull(updated);
        Assert.Equal("Alex Kim", updated.ContactName);
        Assert.Equal("9 Industrial Blvd", updated.Address);
    }
}
