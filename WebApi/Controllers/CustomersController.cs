using Application.Common.Interfaces;
using Application.Dtos.Customers;
using Microsoft.AspNetCore.Mvc;
using Application.Security;
using WebApi.Security;

namespace WebApi.Controllers;

[ApiController]
[Route("api/customers")]

public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.OrdersRead)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetCustomers(CancellationToken ct) =>
        Ok(await customers.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.OrdersRead)]
    public async Task<ActionResult<CustomerDto>> GetCustomer(int id, CancellationToken ct)
    {
        var customer = await customers.GetAsync(id, ct);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    [HasPermission(Permissions.OrdersEdit)]
    public async Task<ActionResult<CustomerDto>> Create(SaveCustomerDto request, CancellationToken ct)
    {
        var customer = (await customers.SaveAsync(null, request, ct))!;
        return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.OrdersEdit)]
    public async Task<ActionResult<CustomerDto>> Update(int id, SaveCustomerDto request, CancellationToken ct)
    {
        var customer = await customers.SaveAsync(id, request, ct);
        return customer is null ? NotFound() : Ok(customer);
    }
}