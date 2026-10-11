using Application.Dtos.Products;

namespace Application.Common.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductListDto>> GetProductsAsync(CancellationToken ct = default);
    Task<ProductListDto?> GetProductAsync(int id, CancellationToken ct = default);
    Task<ProductListDto> CreateProductAsync(UpsertProductDto dto, CancellationToken ct = default);
    Task<ProductListDto?> UpdateProductAsync(int id, UpsertProductDto dto, CancellationToken ct = default);
    Task<ProductStockDto?> GetProductStockAsync(int productId, CancellationToken ct = default);
    Task<ProductStockDto?> UpdateProductStockAsync(int productId, UpdateProductStockDto dto, CancellationToken ct = default);
}
