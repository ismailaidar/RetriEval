using System.Text;

namespace RetriEval.Core;

/// <summary>
/// Renders an <see cref="EvalReport"/> as a GitHub-flavored Markdown document.
/// Suitable as a CI artifact (e.g. appended to a pull-request comment or uploaded as a build artifact).
/// </summary>
public sealed class MarkdownReporter
{
    private readonly MarkdownReporterOptions _options;

    /// <param name="options">Display options. Defaults applied when <see langword="null"/>.</param>
    public MarkdownReporter(MarkdownReporterOptions? options = null)
    {
        _options = options ?? new MarkdownReporterOptions();
    }

    /// <summary>Renders the report to a Markdown string.</summary>
    public string Render(EvalReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();

        WriteHeader(sb, report);
        WriteSummaryTable(sb, report);
        WriteErrorSummary(sb, report);
        WritePerQuerySection(sb, report);

        return sb.ToString();
    }

    /// <summary>Writes the report to <paramref name="path"/>, creating or overwriting the file.</summary>
    public async Task WriteAsync(EvalReport report, string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var markdown = Render(report);
        await File.WriteAllTextAsync(path, markdown, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static void WriteHeader(StringBuilder sb, EvalReport report)
    {
        sb.AppendLine("# RetriEval Report");
        sb.AppendLine();
        sb.AppendLine($"**Run at:** {report.RunAt:u}  ");
        sb.AppendLine($"**Retriever:** `{report.RetrieverName}`  ");
        sb.AppendLine($"**Grader:** `{report.GraderName}`  ");
        sb.AppendLine($"**k:** {report.Options.K}  ");
        sb.AppendLine($"**Cases:** {report.Aggregate.EvaluatedCaseCount} evaluated, {report.Aggregate.ErroredCaseCount} errored");
        sb.AppendLine();
    }

    private void WriteSummaryTable(StringBuilder sb, EvalReport report)
    {
        sb.AppendLine("## Aggregate Metrics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Score | Threshold | Status |");
        sb.AppendLine("|--------|------:|----------:|--------|");

        var agg = report.Aggregate;
        WriteMetricRow(sb, $"Hit@{agg.K}", agg.HitAtK, _options.Thresholds?.HitAtK);
        WriteMetricRow(sb, $"Precision@{agg.K}", agg.MeanPrecisionAtK, _options.Thresholds?.PrecisionAtK);
        WriteMetricRow(sb, $"Recall@{agg.K}", agg.MeanRecallAtK, _options.Thresholds?.RecallAtK);
        WriteMetricRow(sb, "MRR", agg.Mrr, _options.Thresholds?.Mrr);
        WriteMetricRow(sb, "MAP", agg.Map, _options.Thresholds?.Map);
        WriteMetricRow(sb, $"NDCG@{agg.K}", agg.MeanNdcgAtK, _options.Thresholds?.NdcgAtK);
        WriteMetricRow(sb, $"F1@{agg.K}", agg.MeanF1AtK, _options.Thresholds?.F1AtK);

        sb.AppendLine();
    }

    private static void WriteMetricRow(StringBuilder sb, string name, double value, double? threshold)
    {
        var scoreStr = double.IsNaN(value) ? "N/A" : value.ToString("F4");
        var threshStr = threshold.HasValue ? threshold.Value.ToString("F4") : "—";
        var status = threshold.HasValue
            ? (!double.IsNaN(value) && value >= threshold.Value ? "✅ pass" : "❌ fail")
            : "—";
        sb.AppendLine($"| {name} | {scoreStr} | {threshStr} | {status} |");
    }

    private static void WriteErrorSummary(StringBuilder sb, EvalReport report)
    {
        var errors = report.Results.Where(r => r.Error is not null).ToList();
        if (errors.Count == 0) return;

        sb.AppendLine("## Errored Cases");
        sb.AppendLine();
        sb.AppendLine("| Case ID | Error |");
        sb.AppendLine("|---------|-------|");
        foreach (var r in errors)
        {
            var msg = r.Error!.Message.Replace("|", "\\|").Replace("\n", " ");
            sb.AppendLine($"| `{r.CaseId}` | {msg} |");
        }
        sb.AppendLine();
    }

    private static void WritePerQuerySection(StringBuilder sb, EvalReport report)
    {
        sb.AppendLine("## Per-Query Results");
        sb.AppendLine();

        foreach (var result in report.Results)
        {
            sb.AppendLine($"<details>");
            sb.AppendLine($"<summary><b>{result.CaseId}</b> — Hit@k={FormatMetric(result.Metrics.HitAtK)} MRR={FormatMetric(result.Metrics.ReciprocalRank)} NDCG={FormatMetric(result.Metrics.NdcgAtK)}</summary>");
            sb.AppendLine();

            sb.AppendLine($"**Query:** {result.Query}");
            if (result.Category is not null)
                sb.AppendLine($"**Category:** `{result.Category}`");

            if (result.Error is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"> ⚠️ **Error:** {result.Error.Message}");
                sb.AppendLine();
                sb.AppendLine("</details>");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine();
            sb.AppendLine("**Retrieved chunks:**");
            sb.AppendLine();
            sb.AppendLine("| Rank | ID | Score | Relevant |");
            sb.AppendLine("|-----:|:---|------:|:---------|");

            for (var i = 0; i < result.Retrieved.Count; i++)
            {
                var chunk = result.Retrieved[i];
                var rel = i < result.Relevance.Count && result.Relevance[i] ? "✅" : "❌";
                sb.AppendLine($"| {i + 1} | `{chunk.Id}` | {chunk.Score:F4} | {rel} |");
            }

            sb.AppendLine();
            sb.AppendLine("**Metrics:**");
            sb.AppendLine();
            var m = result.Metrics;
            sb.AppendLine($"| Hit@{m.K} | P@{m.K} | R@{m.K} | RR | AP | NDCG@{m.K} | F1@{m.K} |");
            sb.AppendLine($"|-------:|-----:|-----:|----:|----:|--------:|------:|");
            sb.AppendLine($"| {m.HitAtK:F4} | {m.PrecisionAtK:F4} | {m.RecallAtK:F4} | {m.ReciprocalRank:F4} | {m.AveragePrecision:F4} | {m.NdcgAtK:F4} | {m.F1AtK:F4} |");

            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }
    }

    private static string FormatMetric(double v) => double.IsNaN(v) ? "N/A" : v.ToString("F3");
}

/// <summary>Display options for <see cref="MarkdownReporter"/>.</summary>
public sealed record MarkdownReporterOptions
{
    /// <summary>Optional thresholds used to compute pass/fail in the summary table.</summary>
    public MetricThresholds? Thresholds { get; init; }
}

/// <summary>
/// Pass/fail thresholds for each metric. Set a property to <see langword="null"/> to omit the threshold column for that metric.
/// </summary>
public sealed record MetricThresholds
{
    /// <summary>Minimum acceptable Hit@k.</summary>
    public double? HitAtK { get; init; }

    /// <summary>Minimum acceptable mean Precision@k.</summary>
    public double? PrecisionAtK { get; init; }

    /// <summary>Minimum acceptable mean Recall@k.</summary>
    public double? RecallAtK { get; init; }

    /// <summary>Minimum acceptable MRR.</summary>
    public double? Mrr { get; init; }

    /// <summary>Minimum acceptable MAP.</summary>
    public double? Map { get; init; }

    /// <summary>Minimum acceptable mean NDCG@k.</summary>
    public double? NdcgAtK { get; init; }

    /// <summary>Minimum acceptable mean F1@k.</summary>
    public double? F1AtK { get; init; }
}
