using Application.Common.Interfaces;
using Application.Dtos.Customers;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct = default) =>
        await db.Set<CustomerEntity>().AsNoTracking().OrderBy(c => c.FullName)
            .Select(c => new CustomerDto(c.Id, c.FullName, c.Phone, c.Email, c.Address)).ToListAsync(ct);

    public Task<CustomerEntity?> FindAsync(int id, CancellationToken ct = default) =>
        db.Set<CustomerEntity>().FindAsync([id], ct).AsTask();

    public async Task AddAsync(CustomerEntity customer, CancellationToken ct = default) =>
        await db.Set<CustomerEntity>().AddAsync(customer, ct);
}
