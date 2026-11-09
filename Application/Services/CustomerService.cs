using System.ComponentModel.DataAnnotations;
using Application.Common.Interfaces;
using Application.Dtos.Customers;
using Domain.Entities;

namespace Application.Services;

public sealed class CustomerService(ICustomerRepository customers, IUnitOfWork unitOfWork) : ICustomerService
{
    public Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct = default) => customers.GetAllAsync(ct);

    public async Task<CustomerDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var customer = await customers.FindAsync(id, ct);
        return customer is null ? null : Map(customer);
    }

    public async Task<CustomerDto?> SaveAsync(int? id, SaveCustomerDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ValidationException("Customer name is required.");
        var customer = id.HasValue ? await customers.FindAsync(id.Value, ct) : new CustomerEntity();
        if (customer is null) return null;
        customer.FullName = request.FullName.Trim();
        customer.Phone = Clean(request.Phone);
        customer.Email = Clean(request.Email)?.ToLowerInvariant();
        customer.Address = Clean(request.Address);
        if (!id.HasValue) await customers.AddAsync(customer, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(customer);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static CustomerDto Map(CustomerEntity c) => new(c.Id, c.FullName, c.Phone, c.Email, c.Address);
}
