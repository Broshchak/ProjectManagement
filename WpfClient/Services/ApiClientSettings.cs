using System.IO;
using System.Text.Json;

namespace WpfClient.Services;

public sealed class ApiClientSettings
{
    public string BaseUrl { get; set; } = "https://localhost:7001";

    public bool UseMockData { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 10;

    public static ApiClientSettings Load()
    {
        string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(settingsPath))
        {
            return new ApiClientSettings();
        }

        try
        {
            using FileStream stream = File.OpenRead(settingsPath);
            using JsonDocument document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("ApiClient", out JsonElement apiClient))
            {
                return new ApiClientSettings();
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

            return settings;
        }
        catch
        {
            return new ApiClientSettings();
        }
    }
}
