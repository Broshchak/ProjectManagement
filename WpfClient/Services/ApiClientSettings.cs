using System.IO;
using System.Text.Json;

namespace WpfClient.Services;

public sealed class ApiClientSettings
{
    public string BaseUrl { get; set; } = "https://project-management3.runasp.net/";

    public bool UseMockData { get; set; }

    public string? ConfigurationError { get; private set; }

    public int TimeoutSeconds { get; set; } = 10;

    public static ApiClientSettings Load(string? path = null)
    {
        string settingsPath = path ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(settingsPath))
        {
            return new ApiClientSettings { ConfigurationError = "Не знайдено appsettings.json. Відновіть налаштування API." };
        }

        try
        {
            using FileStream stream = File.OpenRead(settingsPath);
            using JsonDocument document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("ApiClient", out JsonElement apiClient))
            {
                throw new InvalidDataException("Відсутній розділ ApiClient.");
            }

            ApiClientSettings settings = new();

            if (apiClient.TryGetProperty("BaseUrl", out JsonElement baseUrl))
            {
                settings.BaseUrl = baseUrl.GetString() ?? settings.BaseUrl;
            }

            if (apiClient.TryGetProperty("UseMockData", out JsonElement useMockData))
            {
                settings.UseMockData = useMockData.GetBoolean();
            }

            if (apiClient.TryGetProperty("TimeoutSeconds", out JsonElement timeoutSeconds))
            {
                settings.TimeoutSeconds = timeoutSeconds.GetInt32();
            }

            if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                settings.TimeoutSeconds is < 1 or > 300)
                throw new InvalidDataException("Некоректна адреса API або таймаут (1–300 секунд).");
            settings.BaseUrl = settings.BaseUrl.TrimEnd('/') + "/";
            return settings;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return new ApiClientSettings { ConfigurationError = $"Помилка appsettings.json: {exception.Message}" };
        }
    }
}
