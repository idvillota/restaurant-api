using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Infrastructure.Persistence;

public sealed class Repository<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    private readonly ApplicationDbContext _db;

    public Repository(ApplicationDbContext db) => _db = db;

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Set<TEntity>().FindAsync([id], cancellationToken).AsTask();

    public IQueryable<TEntity> Query() => _db.Set<TEntity>();

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await _db.Set<TEntity>().AddAsync(entity, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
        await _db.Set<TEntity>().AddRangeAsync(entities, cancellationToken);

    public void Update(TEntity entity) => _db.Set<TEntity>().Update(entity);

    public void Remove(TEntity entity) => _db.Set<TEntity>().Remove(entity);
}
