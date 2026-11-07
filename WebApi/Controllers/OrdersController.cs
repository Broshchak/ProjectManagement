using System.Globalization;
using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Application.Security;
using Microsoft.AspNetCore.Mvc;
using WebApi.Security;

namespace WebApi.Controllers
{
    [Route("api/orders")]
    [ApiController]
    public class OrdersController(IOrderService orderService) : ControllerBase
    {
        [HttpGet]
        [HasPermission(Permissions.OrdersRead)]
        public async Task<IActionResult> GetOrders(CancellationToken ct = default)
        {
            var orders = await orderService.GetOrdersAsync(ct);
            return Ok(orders);
        }

        [HttpGet("{id:int}")]
        [HasPermission(Permissions.OrdersRead)]
        public async Task<IActionResult> GetOrderById(int id, CancellationToken ct = default)
        {
            var order = await orderService.GetOrderByIdAsync(id, ct);
            if (order == null)
            {
                return NotFound();
            }

            return Ok(order);
        }

        [HttpPost]
        [HasPermission(Permissions.OrdersCreate)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto, CancellationToken ct = default)
        {
            var currentUserId = GetCurrentUserId();
            var createdOrder = await orderService.CreateOrderAsync(dto, currentUserId, ct);

            return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder.Id }, createdOrder);
        }

        [HttpPost("{id:int}/items")]
        [HasPermission(Permissions.OrdersEdit)]
        public async Task<IActionResult> AddOrderItem(int id, [FromBody] CreateOrderItemDto dto, CancellationToken ct = default)
        {
            var success = await orderService.AddOrderItemAsync(id, dto, ct);
            if (!success)
            {
                return NotFound();
            }

            return Ok();
        }

        [HttpDelete("{id:int}/items/{itemId:int}")]
        [HasPermission(Permissions.OrdersEdit)]
        public async Task<IActionResult> RemoveOrderItem(int id, int itemId, CancellationToken ct = default)
        {
            var success = await orderService.RemoveOrderItemAsync(id, itemId, ct);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirst(AppClaims.Subject)?.Value;
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? id
                : throw new InvalidOperationException("Authenticated request without a valid subject claim.");
        }
    }
}
