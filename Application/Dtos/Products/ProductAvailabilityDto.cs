namespace Application.Dtos.Products;

public sealed record ProductAvailabilityDto(int ProductId, bool IsActive, int? Stock, decimal? Price);
