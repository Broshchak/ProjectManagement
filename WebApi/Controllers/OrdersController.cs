using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Route("api/orders")]
[ApiController]
public class OrdersController(IOrderService orderService) : ControllerBase
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
        var currentUserId = GetCurrentUserId();
        var createdOrder = await orderService.CreateOrderAsync(dto, currentUserId, ct);
        return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder.Id }, createdOrder);
    }

    [HttpPost("{id:int}/items")]
    public async Task<IActionResult> AddOrderItem(int id, [FromBody] CreateOrderItemDto dto, CancellationToken ct = default)
    {
        var success = await orderService.AddOrderItemAsync(id, dto, ct);
        return success ? Ok() : NotFound(new { message = "Order was not found." });
    }

    [HttpDelete("{id:int}/items/{itemId:int}")]
    public async Task<IActionResult> RemoveOrderItem(int id, int itemId, CancellationToken ct = default)
    {
        var success = await orderService.RemoveOrderItemAsync(id, itemId, ct);
        return success ? NoContent() : NotFound(new { message = "Order item was not found." });
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> ChangeOrderStatus(int id, [FromBody] ChangeOrderStatusDto dto, CancellationToken ct = default)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var order = await orderService.ChangeOrderStatusAsync(id, dto, currentUserId, ct);
            return order is null ? NotFound(new { message = "Order was not found." }) : Ok(order);
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }
}
