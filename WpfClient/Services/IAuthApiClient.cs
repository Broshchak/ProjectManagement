using WpfClient.Models;

namespace WpfClient.Services;

public interface IAuthApiClient
{
    Task<AuthenticatedUser> SignInAsync(string login, string password, CancellationToken cancellationToken = default);

    void SignOut();
}
