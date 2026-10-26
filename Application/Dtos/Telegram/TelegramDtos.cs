using Application.Dtos.Reports;

namespace Application.Dtos.Telegram
{
    // ToString() is overridden on records holding secrets to prevent them from accidentally
    // leaking, for example, through logs.
    public record TelegramLinkDto(string Url, DateTime ExpiresAt)
    {
        public override string ToString() => "TelegramLinkDto { }";
    }

    public record TelegramStatusDto(bool Linked);

    public record BotUserInfo(int UserId, string FullName, string RoleCode);

    public record BotUserDto(int UserId, string FullName, string Role, IReadOnlyCollection<string> Permissions);

    public record RedeemLinkDto(string Token)
    {
        public override string ToString() => "RedeemLinkDto { }";
    }

    public enum LinkRedeemOutcome
    {
        Linked,
        InvalidOrExpired,
        TelegramAccountAlreadyLinked,
        BindFailed
    }

    public record LinkRedeemResult(LinkRedeemOutcome Outcome, BotUserDto? User);

    public enum BotAccess
    {
        Allowed,
        NotLinked,
        Forbidden
    }

    public record BotReportResult(BotAccess Access, ReportOutput? Output);
}
