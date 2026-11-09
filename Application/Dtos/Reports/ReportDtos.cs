namespace Application.Dtos.Reports
{
    public enum ReportChannel
    {
        Desktop,
        Telegram
    }

    public record ReportRequest(int UserId, string RoleCode, ReportChannel Channel);

    public abstract record ReportOutput;

    public sealed record TextReport(string Text) : ReportOutput;

    public sealed record FileReport(string FileName, string ContentType, byte[] Content) : ReportOutput;
}
