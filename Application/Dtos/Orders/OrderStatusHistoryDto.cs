namespace Application.Dtos.Orders
{
    public record OrderStatusHistoryDto(
        string? PreviousStatusCode,
        string? PreviousStatusName,
        string NewStatusCode,
        string NewStatusName,
        int ChangedByUserId,
        string ChangedByUserFullName,
        DateTime ChangedAt,
        string? Comment
    );
}
