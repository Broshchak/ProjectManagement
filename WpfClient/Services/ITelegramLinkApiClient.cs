namespace WpfClient.Services;

public interface ITelegramLinkApiClient
{
    Task<Uri> CreateTelegramLinkAsync(CancellationToken ct = default);
}
