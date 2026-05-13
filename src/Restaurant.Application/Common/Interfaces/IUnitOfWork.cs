namespace Restaurant.Application.Common.Interfaces;

public interface IUnitOfWork
{
    IRepository<TEntity> Repository<TEntity>()
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
