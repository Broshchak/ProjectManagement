namespace Application.Common.Interfaces
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken ct = default);
        Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    }
}
