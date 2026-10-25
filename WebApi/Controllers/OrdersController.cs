using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Microsoft.AspNetCore.Mvc;
using WebApi.Filters;

namespace WebApi.Controllers;

[Route("api/orders")]
[ApiController]
[ServiceErrors]
public class OrdersController(IOrderService orderService, IOrderEditingService editing) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetOrders(CancellationToken ct = default)
    {
        var orders = await orderService.GetOrdersAsync(ct);
        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrderById(int id, CancellationToken ct = default)
    {
        var order = await orderService.GetOrderByIdAsync(id, ct);
        return order is null ? NotFound(new { message = "Order was not found." }) : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto, CancellationToken ct = default)
    {
        try
        {
            var createdOrder = await orderService.CreateOrderAsync(dto, GetCurrentUserId(), ct);
            return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder.Id }, createdOrder);
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/items")]
    public async Task<IActionResult> AddOrderItem(int id, [FromBody] CreateOrderItemDto dto, CancellationToken ct = default)
    {
        return await Edit(() => editing.AddItemAsync(id, dto, ct));
    }

    [HttpDelete("{id:int}/items/{itemId:int}")]
    public async Task<IActionResult> RemoveOrderItem(int id, int itemId, CancellationToken ct = default)
    {
        var result = await Edit(() => editing.RemoveItemAsync(id, itemId, ct));
        return result is OkObjectResult ? NoContent() : result;
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> ChangeOrderStatus(int id, [FromBody] ChangeOrderStatusDto dto, CancellationToken ct = default)
    {
        return await Edit(() => editing.ChangeStatusAsync(id, dto, GetCurrentUserId(), ct));
    }

    [HttpPut("{id:int}")]
    public Task<IActionResult> UpdateOrder(int id, [FromBody] UpdateOrderDto dto, CancellationToken ct = default) =>
        Edit(() => editing.UpdateAsync(id, dto, ct));

    private async Task<IActionResult> Edit(Func<Task<OrderDetailDto?>> action)
    {
        try
        {
            var order = await action();
            return order is null ? NotFound(new { message = "Order or item was not found." }) : Ok(order);
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }
}
