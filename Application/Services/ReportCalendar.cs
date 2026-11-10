namespace Application.Services;

internal static class ReportCalendar
{
    private static readonly TimeZoneInfo Kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");

    public static DateTime InKyiv(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Kyiv);
    public static DateTime ToUtc(DateOnly day) => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Kyiv);

    public static DateTime? Start(DateTime? value, bool kyiv) => value is { } date
        ? kyiv ? ToUtc(DateOnly.FromDateTime(date)) : DateTime.SpecifyKind(date.Date, DateTimeKind.Utc)
        : null;
}
