namespace Application.Common.Interfaces;

public interface IReportPdfRenderer
{
    Task<byte[]> RenderAsync(ReportDocument report, CancellationToken ct);
}

public sealed record ReportDocument(string Title, string Language, DateOnly? From, DateOnly? To, DateTime GeneratedAt,
    bool Landscape, string? Note, string? Context, IReadOnlyList<ReportMetric> Summary, IReadOnlyList<string> Headers,
    IReadOnlyList<ReportTableGroup> Groups);

public sealed record ReportMetric(string Label, string Value);
public sealed record ReportTableGroup(string? Label, IReadOnlyList<IReadOnlyList<string>> Rows);
