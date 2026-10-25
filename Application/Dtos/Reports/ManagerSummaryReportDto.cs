namespace Application.Dtos.Reports;

public sealed record ManagerSummaryReportDto(
    DateTime GeneratedAt,
    string Text);
