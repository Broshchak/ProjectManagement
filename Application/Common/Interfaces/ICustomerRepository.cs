using Application.Dtos.Customers;
using Domain.Entities;

namespace Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct = default);
    Task<CustomerEntity?> FindAsync(int id, CancellationToken ct = default);
    Task AddAsync(CustomerEntity customer, CancellationToken ct = default);
}
