using Application.Common.Interfaces;
using Application.Dtos.Products;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(AppDbContext context) : IProductRepository
{
    public async Task<IEnumerable<ProductListDto>> GetProductsAsync(CancellationToken ct = default)
    {
        return await QueryProducts(orderByName: true).ToListAsync(ct);
    }

    public async Task<ProductListDto?> GetProductAsync(int id, CancellationToken ct = default)
    {
        return await QueryProducts(productId: id).FirstOrDefaultAsync(ct);
    }

    public async Task<ProductEntity?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await context.Set<ProductEntity>()
            .FirstOrDefaultAsync(product => product.Id == id, ct);
    }

    public async Task<ProductCategoryEntity?> GetCategoryByNameAsync(string name, CancellationToken ct = default)
    {
        return await context.Set<ProductCategoryEntity>()
            .FirstOrDefaultAsync(category => category.Name == name, ct);
    }

    public async Task AddCategoryAsync(ProductCategoryEntity category, CancellationToken ct = default)
    {
        await context.Set<ProductCategoryEntity>().AddAsync(category, ct);
    }

    public async Task AddProductAsync(ProductEntity product, CancellationToken ct = default)
    {
        await context.Set<ProductEntity>().AddAsync(product, ct);
    }

    public async Task<ProductPriceEntity?> GetPriceAsync(int productId, CancellationToken ct = default)
    {
        return await context.Set<ProductPriceEntity>()
            .FirstOrDefaultAsync(price => price.ProductId == productId, ct);
    }

    public async Task AddPriceAsync(ProductPriceEntity price, CancellationToken ct = default)
    {
        await context.Set<ProductPriceEntity>().AddAsync(price, ct);
    }

    public async Task<ProductStockEntity?> GetStockAsync(int productId, CancellationToken ct = default)
    {
        return await context.Set<ProductStockEntity>()
            .FirstOrDefaultAsync(stock => stock.ProductId == productId, ct);
    }

    public async Task AddStockAsync(ProductStockEntity stock, CancellationToken ct = default)
    {
        await context.Set<ProductStockEntity>().AddAsync(stock, ct);
    }

    private IQueryable<ProductListDto> QueryProducts(int? productId = null, bool orderByName = false)
    {
        IQueryable<ProductEntity> products = context.Set<ProductEntity>().AsNoTracking();

        if (productId.HasValue)
        {
            products = products.Where(product => product.Id == productId.Value);
        }

        if (orderByName)
        {
            products = products.OrderBy(product => product.Name);
        }

        return from p in products
            join c in context.Set<ProductCategoryEntity>().AsNoTracking() on p.CategoryId equals c.Id
            join pp in context.Set<ProductPriceEntity>().AsNoTracking() on p.Id equals pp.ProductId into prices
            from price in prices.DefaultIfEmpty()
            join ps in context.Set<ProductStockEntity>().AsNoTracking() on p.Id equals ps.ProductId into stocks
            from stock in stocks.DefaultIfEmpty()
            select new ProductListDto(
                p.Id,
                p.Name,
                c.Name,
                price != null ? price.Price : 0m,
                price != null ? price.CurrencyCode : "UAH",
                stock != null ? stock.Quantity : 0,
                p.IsActive);
    }
}
