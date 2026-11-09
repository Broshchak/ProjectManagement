using System.ComponentModel.DataAnnotations;
using Application.Common.Interfaces;
using Application.Dtos.Orders;
using Domain.Entities;

namespace Application.Services
{
    public class OrderService(IOrderRepository orderRepository, IProductRepository products,
        IUnitOfWork unitOfWork, OrderWorkflowService workflow) : IOrderService
    {
        public async Task<IEnumerable<OrderSummaryDto>> GetOrdersAsync(CancellationToken ct = default)
        {
            return await orderRepository.GetSummariesAsync(ct);
        }

        public async Task<OrderDetailDto?> GetOrderByIdAsync(int id, CancellationToken ct = default)
        {
            return await orderRepository.GetDetailByIdAsync(id, ct);
        }

        public Task<OrderDetailDto> CreateOrderAsync(CreateOrderDto dto, int currentUserId, CancellationToken ct = default) =>
            unitOfWork.InTransactionAsync(token => CreateAsync(dto, currentUserId, token), ct);

        private async Task<OrderDetailDto> CreateAsync(CreateOrderDto dto, int currentUserId, CancellationToken ct)
        {
            var initialCode = await workflow.GetInitialStateAsync(ct);
            if (!await orderRepository.CustomerExistsAsync(dto.CustomerId, ct))
                throw new ValidationException("Customer was not found.");
            if (dto.Items == null || dto.Items.Count == 0 || dto.Items.Any(i => i.Quantity <= 0))
            {
                throw new ValidationException("An order must contain at least one item with a positive quantity.");
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

            var initialStatus = await orderRepository.GetStatusByCodeAsync(initialCode, ct)
                ?? throw new InvalidOperationException("The initial workflow state was not found.");

            var availability = (await products.GetAvailabilityAsync(dto.Items.Select(i => i.ProductId).ToArray(), ct))
                .ToDictionary(product => product.ProductId);

            foreach (var item in dto.Items)
            {
                if (!availability.TryGetValue(item.ProductId, out var product) || !product.IsActive)
                    throw new ValidationException($"Product {item.ProductId} is unavailable.");
                if (product.Stock is not int stock || item.Quantity > stock)
                    throw new ValidationException($"Insufficient stock for product {item.ProductId}.");
                if (product.Price is null or <= 0)
                    throw new ValidationException($"Price for product {item.ProductId} was not found.");
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
                    UnitPrice = availability[itemDto.ProductId].Price!.Value
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
