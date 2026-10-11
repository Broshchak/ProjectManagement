using Application.Common.Interfaces;
using Application.Dtos.Products;
using Domain.Entities;

namespace Application.Services;

public sealed class ProductService(IProductRepository productRepository, IUnitOfWork unitOfWork) : IProductService
{
    public async Task<IEnumerable<ProductListDto>> GetProductsAsync(CancellationToken ct = default)
    {
        return await productRepository.GetProductsAsync(ct);
    }

    public async Task<ProductListDto?> GetProductAsync(int id, CancellationToken ct = default)
    {
        return await productRepository.GetProductAsync(id, ct);
    }

    public async Task<ProductListDto> CreateProductAsync(UpsertProductDto dto, CancellationToken ct = default)
    {
        ValidateProduct(dto);

        ProductCategoryEntity category = await GetOrCreateCategoryAsync(dto.CategoryName, ct);
        ProductEntity product = new()
        {
            Name = dto.Name.Trim(),
            Category = category,
            IsActive = dto.IsActive
        };

        await productRepository.AddProductAsync(product, ct);
        await productRepository.AddPriceAsync(new ProductPriceEntity
        {
            Product = product,
            Price = dto.Price,
            CurrencyCode = "UAH"
        }, ct);
        await productRepository.AddStockAsync(new ProductStockEntity
        {
            Product = product,
            Quantity = dto.Quantity
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return await productRepository.GetProductAsync(product.Id, ct)
            ?? throw new InvalidOperationException("Created product was not found.");
    }

    public async Task<ProductListDto?> UpdateProductAsync(int id, UpsertProductDto dto, CancellationToken ct = default)
    {
        ValidateProduct(dto);

        ProductEntity? product = await productRepository.GetByIdAsync(id, ct);
        if (product is null)
        {
            return null;
        }

        ProductCategoryEntity category = await GetOrCreateCategoryAsync(dto.CategoryName, ct);
        product.Name = dto.Name.Trim();
        product.CategoryId = category.Id;
        product.Category = category;
        product.IsActive = dto.IsActive;

        ProductPriceEntity? price = await productRepository.GetPriceAsync(id, ct);
        if (price is null)
        {
            await productRepository.AddPriceAsync(new ProductPriceEntity
            {
                ProductId = id,
                Price = dto.Price,
                CurrencyCode = "UAH"
            }, ct);
        }
        else
        {
            price.Price = dto.Price;
            price.CurrencyCode = "UAH";
        }

        ProductStockEntity? stock = await productRepository.GetStockAsync(id, ct);
        if (stock is null)
        {
            await productRepository.AddStockAsync(new ProductStockEntity
            {
                ProductId = id,
                Quantity = dto.Quantity
            }, ct);
        }
        else
        {
            stock.Quantity = dto.Quantity;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return await productRepository.GetProductAsync(id, ct);
    }

    public async Task<ProductStockDto?> GetProductStockAsync(int productId, CancellationToken ct = default)
    {
        ProductEntity? product = await productRepository.GetByIdAsync(productId, ct);
        if (product is null)
        {
            return null;
        }

        ProductStockEntity? stock = await productRepository.GetStockAsync(productId, ct);
        return new ProductStockDto(productId, stock?.Quantity ?? 0);
    }

    public async Task<ProductStockDto?> UpdateProductStockAsync(int productId, UpdateProductStockDto dto, CancellationToken ct = default)
    {
        if (dto.Quantity < 0)
        {
            throw new InvalidOperationException("Product quantity cannot be less than 0.");
        }

        ProductEntity? product = await productRepository.GetByIdAsync(productId, ct);
        if (product is null)
        {
            return null;
        }

        ProductStockEntity? stock = await productRepository.GetStockAsync(productId, ct);
        if (stock is null)
        {
            await productRepository.AddStockAsync(new ProductStockEntity
            {
                ProductId = productId,
                Quantity = dto.Quantity
            }, ct);
        }
        else
        {
            stock.Quantity = dto.Quantity;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new ProductStockDto(productId, dto.Quantity);
    }

    private async Task<ProductCategoryEntity> GetOrCreateCategoryAsync(string categoryName, CancellationToken ct)
    {
        string normalizedCategoryName = categoryName.Trim();
        ProductCategoryEntity? category = await productRepository.GetCategoryByNameAsync(normalizedCategoryName, ct);
        if (category is not null)
        {
            return category;
        }

        category = new ProductCategoryEntity
        {
            Name = normalizedCategoryName,
            IsActive = true
        };

        await productRepository.AddCategoryAsync(category, ct);
        return category;
    }

    private static void ValidateProduct(UpsertProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new InvalidOperationException("Product name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.CategoryName))
        {
            throw new InvalidOperationException("Product category is required.");
        }

        if (dto.Price <= 0)
        {
            throw new InvalidOperationException("Product price must be greater than 0.");
        }

        if (dto.Quantity < 0)
        {
            throw new InvalidOperationException("Product quantity cannot be less than 0.");
        }
    }
}
