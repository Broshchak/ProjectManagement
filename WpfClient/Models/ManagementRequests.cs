namespace WpfClient.Models;

public sealed class SaveCustomerRequest
{
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public sealed record UserAccount(int Id, string Login, string FullName, string Role, bool IsActive, bool TelegramAllowed, long? TelegramUserId);
public sealed record SaveUserRequest(string Login, string FullName, string Role, bool IsActive, bool TelegramAllowed, long? TelegramUserId);
public sealed record CreateUserRequest(string Login, string FullName, string Role, bool IsActive, bool TelegramAllowed, long? TelegramUserId, string Password);
public sealed record UpdateOrderRequest(string ExpectedVersion, string? Comment, IReadOnlyList<UpdateOrderItemRequest> Items);
public sealed record UpdateOrderItemRequest(int Id, int ProductId, int Quantity);
