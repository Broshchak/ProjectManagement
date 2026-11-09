using System.Globalization;
using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Application.Security;
using Microsoft.AspNetCore.Mvc;
using WebApi.Security;

namespace WebApi.Controllers;

[Route("api/orders")]
[ApiController]
public sealed class OrdersController(IOrderService orders, IOrderEditingService editing) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.OrdersRead)]
    public async Task<IActionResult> GetOrders(CancellationToken ct) => Ok(await orders.GetOrdersAsync(ct));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.OrdersRead)]
    public async Task<IActionResult> GetOrderById(int id, CancellationToken ct) =>
        await orders.GetOrderByIdAsync(id, ct) is { } order ? Ok(order) : NotFound();

    [HttpPost]
    [HasPermission(Permissions.OrdersCreate)]
    public async Task<IActionResult> CreateOrder(CreateOrderDto dto, CancellationToken ct)
    {
        var order = await orders.CreateOrderAsync(dto, CurrentUserId, ct);
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    [HttpPost("{id:int}/items")]
    [HasPermission(Permissions.OrdersEdit)]
    public async Task<IActionResult> AddOrderItem(int id, CreateOrderItemDto dto, CancellationToken ct) =>
        await editing.AddItemAsync(id, dto, ct) is { } order ? Ok(order) : NotFound();

    [HttpDelete("{id:int}/items/{itemId:int}")]
    [HasPermission(Permissions.OrdersEdit)]
    public async Task<IActionResult> RemoveOrderItem(int id, int itemId, CancellationToken ct) =>
        await editing.RemoveItemAsync(id, itemId, ct) is not null ? NoContent() : NotFound();

    [HttpPost("{id:int}/status")]
    [HasPermission(Permissions.OrdersEdit)]
    public async Task<IActionResult> ChangeOrderStatus(int id, ChangeOrderStatusDto dto, CancellationToken ct) =>
        await editing.ChangeStatusAsync(id, dto, CurrentUserId, ct) is { } order ? Ok(order) : NotFound();

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.OrdersEdit)]
    public async Task<IActionResult> UpdateOrder(int id, UpdateOrderDto dto, CancellationToken ct) =>
        await editing.UpdateAsync(id, dto, ct) is { } order ? Ok(order) : NotFound();

    private int CurrentUserId =>
        int.Parse(User.FindFirst(AppClaims.Subject)!.Value, CultureInfo.InvariantCulture);
}
