using Application.Common.Interfaces;
using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class UnitOfWork(AppDbContext context) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            try { return await context.SaveChangesAsync(ct); }
            catch (Exception ex) when (PersistenceErrors.IsConflict(ex))
            { throw PersistenceErrors.Translate(ex); }
        }

        public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
        {
            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var result = await action(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (Exception ex) when (PersistenceErrors.IsConflict(ex))
            { throw PersistenceErrors.Translate(ex); }
        }
    }
}
