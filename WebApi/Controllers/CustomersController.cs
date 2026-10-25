using Application.Dtos.Customers;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace WebApi.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetCustomers(CancellationToken ct) =>
        await db.Set<CustomerEntity>().AsNoTracking().OrderBy(c => c.FullName)
            .Select(c => new CustomerDto(c.Id, c.FullName, c.Phone, c.Email, c.Address)).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(int id, CancellationToken ct)
    {
        var customer = await db.Set<CustomerEntity>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return customer is null ? NotFound() : Ok(Map(customer));
    }

    [HttpPost]
    public Task<ActionResult<CustomerDto>> Create(SaveCustomerDto request, CancellationToken ct) => Save(null, request, ct);

    [HttpPut("{id:int}")]
    public Task<ActionResult<CustomerDto>> Update(int id, SaveCustomerDto request, CancellationToken ct) => Save(id, request, ct);

    private async Task<ActionResult<CustomerDto>> Save(int? id, SaveCustomerDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)) return BadRequest(new { message = "Customer name is required." });
        var customer = id.HasValue ? await db.Set<CustomerEntity>().FindAsync([id.Value], ct) : new CustomerEntity();
        if (customer is null) return NotFound();
        customer.FullName = request.FullName.Trim();
        customer.Phone = Clean(request.Phone);
        customer.Email = Clean(request.Email)?.ToLowerInvariant();
        customer.Address = Clean(request.Address);
        if (!id.HasValue) db.Add(customer);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { message = "A customer with this email already exists." }); }
        return id.HasValue ? Ok(Map(customer)) : CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, Map(customer));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static CustomerDto Map(CustomerEntity c) => new(c.Id, c.FullName, c.Phone, c.Email, c.Address);
}
