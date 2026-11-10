namespace Orders.Reports;

public sealed record ReportLabel(string Code, string Ukrainian, string English)
{
    public string In(string language) => language == "en" ? English : Ukrainian;
}

public sealed record ReportLayout(string Source, IReadOnlyList<string> Columns, string? SortBy = null,
    bool Descending = false, string? GroupBy = null, bool ShowSummary = true, bool ShowTable = true,
    string? Note = null, bool Landscape = false, string? Status = null, string? Category = null,
    DateOnly? DefaultFrom = null, DateOnly? DefaultTo = null, int SchemaVersion = 1);

public sealed record ReportSource(ReportLabel Label, IReadOnlyList<ReportLabel> Fields, IReadOnlyList<string> Groups,
    ReportLayout DefaultLayout);

public sealed record ReportTemplate(int Id, string Name, int OwnerUserId, long Version, ReportLayout Layout);

public sealed record SaveReportTemplate(string Name, ReportLayout Layout, long? ExpectedVersion = null);

public sealed record GenerateReportPdf(string? Preset = null, int? TemplateId = null, long? ExpectedVersion = null,
    ReportLayout? Draft = null, string? Title = null, DateOnly? From = null, DateOnly? To = null, string Language = "uk");
