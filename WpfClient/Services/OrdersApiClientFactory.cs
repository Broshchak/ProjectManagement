using System.Net.Http;

namespace WpfClient.Services;

public static class OrdersApiClientFactory
{
    public static IOrdersApiClient Create(ApiClientSettings settings)
    {
        if (settings.UseMockData)
        {
            return new MockOrdersApiClient();
        }

        HttpClient httpClient = new()
        {
            BaseAddress = new Uri(settings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
        };

        return new HttpOrdersApiClient(httpClient);
    }
}
