using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Persistence;

public sealed class RepositoryTests
{
    [Fact]
    public async Task Add_and_unit_of_work_save_persist_entity()
    {
        using var fx = new TenantDbFixture();
        var repo = fx.Repository<Ingredient>();
        await repo.AddAsync(
            new Ingredient
            {
                Id = Guid.NewGuid(),
                Name = "Test ingredient",
                Unit = IngredientUnit.Unit,
                IsActive = true,
            },
            CancellationToken.None);
        await fx.UnitOfWork.SaveChangesAsync();

        Assert.Equal(1, await fx.Db.Ingredients.CountAsync());
    }

    [Fact]
    public async Task GetByIdAsync_returns_tracked_entity()
    {
        using var fx = new TenantDbFixture();
        var id = Guid.NewGuid();
        fx.Db.Ingredients.Add(
            new Ingredient
            {
                Id = id,
                TenantId = fx.TenantId,
                Name = "Tracked",
                Unit = IngredientUnit.Gram,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();
        fx.Db.ChangeTracker.Clear();

        var repo = new Repository<Ingredient>(fx.Db);
        var entity = await repo.GetByIdAsync(id);

        Assert.NotNull(entity);
        Assert.Equal("Tracked", entity!.Name);
    }
}
