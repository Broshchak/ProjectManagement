using Application.Dtos.Products;
using Domain.Entities;

namespace Application.Common.Interfaces;

public interface IProductRepository
{
    Task<IReadOnlyList<ProductAvailabilityDto>> GetAvailabilityAsync(IReadOnlyCollection<int> productIds, CancellationToken ct = default);
    Task<IEnumerable<ProductListDto>> GetProductsAsync(CancellationToken ct = default);
    Task<ProductListDto?> GetProductAsync(int id, CancellationToken ct = default);
    Task<ProductEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProductCategoryEntity?> GetCategoryByNameAsync(string name, CancellationToken ct = default);
    Task AddCategoryAsync(ProductCategoryEntity category, CancellationToken ct = default);
    Task AddProductAsync(ProductEntity product, CancellationToken ct = default);
    Task<ProductPriceEntity?> GetPriceAsync(int productId, CancellationToken ct = default);
    Task AddPriceAsync(ProductPriceEntity price, CancellationToken ct = default);
    Task<ProductStockEntity?> GetStockAsync(int productId, CancellationToken ct = default);
    Task AddStockAsync(ProductStockEntity stock, CancellationToken ct = default);
}
