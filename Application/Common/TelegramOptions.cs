namespace Application.Common
{
    public sealed class TelegramOptions
    {
        public string BotUsername { get; init; } = string.Empty;
        public int LinkTtlMinutes { get; init; } = 5;

        public bool IsConfigured => BotUsername.Length > 0;
    }
}
