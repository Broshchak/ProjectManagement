using WpfClient.Models;

namespace WpfClient.Services;

public interface IManagementApiClient
{
    Task<IReadOnlyList<CustomerListItem>> GetCustomersAsync(CancellationToken ct = default);
    Task<CustomerListItem> SaveCustomerAsync(int? id, SaveCustomerRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken ct = default);
    Task<UserAccount> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserAccount> UpdateUserAsync(int id, SaveUserRequest request, CancellationToken ct = default);
    Task ChangeUserPasswordAsync(int id, string password, CancellationToken ct = default);
}

public interface IOrderEditingApiClient
{
    Task<OrderDetail> UpdateOrderAsync(int id, UpdateOrderRequest request, CancellationToken ct = default);
}
