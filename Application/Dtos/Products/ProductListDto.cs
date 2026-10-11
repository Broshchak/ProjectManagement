namespace Application.Dtos.Products;

public sealed record ProductListDto(
    int Id,
    string Name,
    string CategoryName,
    decimal Price,
    string CurrencyCode,
    int Quantity,
    bool IsActive);

public sealed record UpsertProductDto(
    string Name,
    string CategoryName,
    decimal Price,
    int Quantity,
    bool IsActive);

public sealed record ProductStockDto(
    int ProductId,
    int Quantity);

public sealed record UpdateProductStockDto(
    int Quantity);
