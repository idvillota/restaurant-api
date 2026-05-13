using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(ApplicationDbContext db) => _db = db;

    public IRepository<TEntity> Repository<TEntity>()
        where TEntity : class
    {
        var type = typeof(TEntity);
        if (!_repositories.TryGetValue(type, out var repo))
        {
            repo = new Repository<TEntity>(_db);
            _repositories[type] = repo;
        }

        return (IRepository<TEntity>)repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
