using System.Text.RegularExpressions;
using Application.Common;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Telegram
{
    public static class TelegramOptionsFactory
    {
        public static TelegramOptions FromConfiguration(IConfiguration configuration)
        {
            var username = (configuration["Telegram:BotUsername"] ?? string.Empty).Trim().TrimStart('@');
            if (username.Length == 0)
            {
                return new TelegramOptions(); // linking disabled: link endpoints answer 503
            }

            if (!Regex.IsMatch(username, "^[A-Za-z][A-Za-z0-9_]{4,31}$"))
            {
                throw new InvalidOperationException("Telegram:BotUsername is invalid (5-32 chars: letters, digits, underscore; no @).");
            }

            var ttl = int.TryParse(configuration["Telegram:LinkTtlMinutes"], out var minutes) && minutes is >= 1 and <= 30
                ? minutes
                : 5;

            return new TelegramOptions { BotUsername = username, LinkTtlMinutes = ttl };
        }
    }
}
