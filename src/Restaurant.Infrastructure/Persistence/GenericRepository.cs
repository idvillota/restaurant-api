using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Infrastructure.Persistence;

public sealed class GenericRepository<T> : IRepository<T> where T : class
{
    private readonly ApplicationDbContext _db;

    public GenericRepository(ApplicationDbContext db) => _db = db;

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Set<T>().FindAsync([id], cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.Set<T>().AsNoTracking().ToListAsync(cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await _db.Set<T>().AddAsync(entity, cancellationToken);

    public void Update(T entity) => _db.Set<T>().Update(entity);

    public void Remove(T entity) => _db.Set<T>().Remove(entity);
}
