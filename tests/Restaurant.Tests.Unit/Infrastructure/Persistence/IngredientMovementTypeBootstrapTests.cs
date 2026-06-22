using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Restaurant.Infrastructure.Persistence.Seeding;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Persistence;

public sealed class IngredientMovementTypeBootstrapTests
{
    [Fact]
    public async Task EnsureForTenantAsync_inserts_defaults_once()
    {
        using var fx = new TenantDbFixture();

        var first = await IngredientMovementTypeBootstrap.EnsureForTenantAsync(fx.Db, fx.TenantId);
        await fx.Db.SaveChangesAsync();
        var second = await IngredientMovementTypeBootstrap.EnsureForTenantAsync(fx.Db, fx.TenantId);

        Assert.Equal(5, first);
        Assert.Equal(0, second);
        Assert.Equal(5, await fx.Db.IngredientMovementTypes.IgnoreQueryFilters().CountAsync());
        Assert.True(await fx.Db.IngredientMovementTypes.IgnoreQueryFilters()
            .AnyAsync(t => t.Name == "Ingreso por regalo" && t.IsInput));
    }
}
