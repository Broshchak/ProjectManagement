using System.ComponentModel.DataAnnotations;
using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Domain.Entities;

namespace Application.Services
{
    public class OrderService(IOrderRepository orderRepository, IUnitOfWork unitOfWork) : IOrderService
    {
        public async Task<IEnumerable<OrderSummaryDto>> GetOrdersAsync(CancellationToken ct = default)
        {
            return await orderRepository.GetSummariesAsync(ct);
        }

        public async Task<OrderDetailDto?> GetOrderByIdAsync(int id, CancellationToken ct = default)
        {
            return await orderRepository.GetDetailByIdAsync(id, ct);
        }

        public async Task<OrderDetailDto> CreateOrderAsync(CreateOrderDto dto, int currentUserId, CancellationToken ct = default)
        {
            if (dto.Items == null || dto.Items.Count == 0)
            {
                throw new ValidationException("An order must contain at least one item.");
            }

            var duplicateProductIds = dto.Items
                .GroupBy(i => i.ProductId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateProductIds.Count > 0)
            {
                throw new ValidationException($"Duplicate products detected in order request for Product IDs: {string.Join(", ", duplicateProductIds)}.");
            }

            var initialStatus = await orderRepository.GetStatusByCodeAsync("NewOrder", ct)
                ?? throw new InvalidOperationException("Initial order status 'NewOrder' was not found.");

            var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
            var priceMap = await orderRepository.GetProductPricesAsync(productIds, ct);

            foreach (var productId in productIds)
            {
                if (!priceMap.ContainsKey(productId))
                {
                    throw new InvalidOperationException($"Price for Product ID {productId} was not found.");
                }
            }

            var order = new OrderEntity
            {
                OrderNumber = dto.OrderNumber,
                StatusId = initialStatus.Id,
                CustomerId = dto.CustomerId,
                CreatedByUserId = currentUserId,
                Comment = dto.Comment
            };

            await orderRepository.AddAsync(order, ct);

            foreach (var itemDto in dto.Items)
            {
                var item = new OrderItemEntity
                {
                    Order = order,
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = priceMap[itemDto.ProductId]
                };
                await orderRepository.AddItemAsync(item, ct);
            }

            var history = new OrderStatusHistoryEntity
            {
                Order = order,
                PreviousStatusId = null,
                NewStatusId = initialStatus.Id,
                ChangedByUserId = currentUserId,
                Comment = "Initial order creation"
            };
            await orderRepository.AddStatusHistoryAsync(history, ct);

            await unitOfWork.SaveChangesAsync(ct);

            return (await orderRepository.GetDetailByIdAsync(order.Id, ct))!;
        }

        public async Task<bool> AddOrderItemAsync(int orderId, CreateOrderItemDto dto, CancellationToken ct = default)
        {
            var order = await orderRepository.GetByIdAsync(orderId, ct);
            if (order == null) return false;

            var unitPrice = await orderRepository.GetProductPriceAsync(dto.ProductId, ct)
                ?? throw new InvalidOperationException($"Price for product ID {dto.ProductId} was not found.");

            var item = new OrderItemEntity
            {
                Order = order,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                UnitPrice = unitPrice
            };

            await orderRepository.AddItemAsync(item, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> RemoveOrderItemAsync(int orderId, int itemId, CancellationToken ct = default)
        {
            var item = await orderRepository.GetItemByIdAsync(orderId, itemId, ct);
            if (item == null) return false;

            orderRepository.RemoveItem(item);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }
    }
}
