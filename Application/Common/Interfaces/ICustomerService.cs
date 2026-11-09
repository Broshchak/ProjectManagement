using Application.Dtos.Customers;

namespace Application.Common.Interfaces;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct = default);
    Task<CustomerDto?> GetAsync(int id, CancellationToken ct = default);
    Task<CustomerDto?> SaveAsync(int? id, SaveCustomerDto request, CancellationToken ct = default);
}
