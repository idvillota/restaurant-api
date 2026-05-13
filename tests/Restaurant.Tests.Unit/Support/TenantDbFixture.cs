using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Mapping;
using Restaurant.Infrastructure.Common;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Tests.Unit.Support;

/// <summary>
/// Shared EF Core in-memory database + tenant context + AutoMapper for service-level tests.
/// </summary>
public sealed class TenantDbFixture : IDisposable
{
    public Guid TenantId { get; }
    public CurrentTenantContext TenantContext { get; }
    public ApplicationDbContext Db { get; }
    public IMapper Mapper { get; }

    public TenantDbFixture()
    {
        TenantId = Guid.NewGuid();
        TenantContext = new CurrentTenantContext { TenantId = TenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new ApplicationDbContext(options, TenantContext);
        Mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<IngredientMappingProfile>();
            cfg.AddProfile<ProductMappingProfile>();
        }).CreateMapper();
    }

    public IRepository<T> Repository<T>()
        where T : class =>
        new Repository<T>(Db);

    public IUnitOfWork UnitOfWork => new UnitOfWork(Db);

    public void Dispose() => Db.Dispose();
}

/// <summary>
/// In-memory database without tenant resolution (registration / login flows).
/// </summary>
public sealed class NoTenantDbFixture : IDisposable
{
    public ApplicationDbContext Db { get; }
    public CurrentTenantContext TenantContext { get; }
    public IMapper Mapper { get; }

    public NoTenantDbFixture()
    {
        TenantContext = new CurrentTenantContext();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new ApplicationDbContext(options, TenantContext);
        Mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<IngredientMappingProfile>();
            cfg.AddProfile<ProductMappingProfile>();
        }).CreateMapper();
    }

    public IUnitOfWork UnitOfWork => new UnitOfWork(Db);

    public void Dispose() => Db.Dispose();
}
