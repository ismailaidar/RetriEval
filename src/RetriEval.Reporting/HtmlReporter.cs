using System.Net;
using System.Text;
using RetriEval.Core;

namespace RetriEval.Reporting;

/// <summary>
/// Renders an <see cref="EvalReport"/> as a self-contained HTML document.
/// The output is a single file with inline CSS — no external assets, CDN calls, or JavaScript frameworks.
/// Suitable for attaching to a PR, emailing, or archiving without hosting.
/// </summary>
public sealed class HtmlReporter
{
    /// <summary>Renders the report to an HTML string.</summary>
    public string Render(EvalReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        WriteDocument(sb, report);
        return sb.ToString();
    }

    /// <summary>Writes the HTML to <paramref name="path"/>, creating or overwriting the file.</summary>
    public async Task WriteAsync(EvalReport report, string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var html = Render(report);
        await File.WriteAllTextAsync(path, html, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static void WriteDocument(StringBuilder sb, EvalReport report)
    {
        var agg = report.Aggregate;
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.AppendLine($"<title>RetriEval Report — {H(report.RetrieverName)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(Css());
        sb.AppendLine("</style>");
        sb.AppendLine("</head><body>");

        // Header
        sb.AppendLine("<header>");
        sb.AppendLine("<h1>RetriEval Report</h1>");
        sb.AppendLine("<dl class=\"meta\">");
        sb.AppendLine($"<dt>Run at</dt><dd>{H(report.RunAt.ToString("u"))}</dd>");
        sb.AppendLine($"<dt>Retriever</dt><dd><code>{H(report.RetrieverName)}</code></dd>");
        sb.AppendLine($"<dt>Grader</dt><dd><code>{H(report.GraderName)}</code></dd>");
        sb.AppendLine($"<dt>k</dt><dd>{agg.K}</dd>");
        sb.AppendLine($"<dt>Cases</dt><dd>{agg.EvaluatedCaseCount} evaluated, {agg.ErroredCaseCount} errored</dd>");
        sb.AppendLine("</dl>");
        sb.AppendLine("</header>");

        // Aggregate table
        sb.AppendLine("<section>");
        sb.AppendLine("<h2>Aggregate Metrics</h2>");
        sb.AppendLine("<table><thead><tr><th>Metric</th><th>Score</th></tr></thead><tbody>");
        AggRow(sb, $"Hit@{agg.K}", agg.HitAtK);
        AggRow(sb, $"Precision@{agg.K}", agg.MeanPrecisionAtK);
        AggRow(sb, $"Recall@{agg.K}", agg.MeanRecallAtK);
        AggRow(sb, "MRR", agg.Mrr);
        AggRow(sb, "MAP", agg.Map);
        AggRow(sb, $"NDCG@{agg.K}", agg.MeanNdcgAtK);
        AggRow(sb, $"F1@{agg.K}", agg.MeanF1AtK);
        sb.AppendLine("</tbody></table>");
        sb.AppendLine("</section>");

        // Per-query drill-down
        sb.AppendLine("<section>");
        sb.AppendLine("<h2>Per-Query Results</h2>");
        foreach (var result in report.Results)
            WriteQueryCard(sb, result, agg.K);
        sb.AppendLine("</section>");

        sb.AppendLine("</body></html>");
    }

    private static void WriteQueryCard(StringBuilder sb, QueryResult result, int k)
    {
        var isError = result.Error is not null;
        var cls = isError ? "card error" : "card";
        sb.AppendLine($"<details class=\"{cls}\">");
        sb.AppendLine($"<summary><strong>{H(result.CaseId)}</strong>");
        if (!isError)
        {
            sb.AppendLine($"<span class=\"badge\">Hit@{k}={result.Metrics.HitAtK:F3}</span>");
            sb.AppendLine($"<span class=\"badge\">MRR={result.Metrics.ReciprocalRank:F3}</span>");
            sb.AppendLine($"<span class=\"badge\">NDCG={result.Metrics.NdcgAtK:F3}</span>");
        }
        sb.AppendLine("</summary>");
        sb.AppendLine($"<p><strong>Query:</strong> {H(result.Query)}</p>");
        if (result.Category is not null)
            sb.AppendLine($"<p><strong>Category:</strong> <code>{H(result.Category)}</code></p>");

        if (isError)
        {
            sb.AppendLine($"<p class=\"err\">⚠ Error: {H(result.Error!.Message)}</p>");
        }
        else
        {
            // Retrieved chunks
            sb.AppendLine("<table><thead><tr><th>Rank</th><th>ID</th><th>Score</th><th>Relevant</th></tr></thead><tbody>");
            for (var i = 0; i < result.Retrieved.Count; i++)
            {
                var chunk = result.Retrieved[i];
                var rel = i < result.Relevance.Count && result.Relevance[i];
                var relCell = rel ? "<td class=\"yes\">✓</td>" : "<td class=\"no\">✗</td>";
                sb.AppendLine($"<tr><td>{i + 1}</td><td><code>{H(chunk.Id)}</code></td><td>{chunk.Score:F4}</td>{relCell}</tr>");
            }
            sb.AppendLine("</tbody></table>");

            // Per-query metrics
            var m = result.Metrics;
            sb.AppendLine("<table class=\"metrics\"><thead><tr>");
            sb.AppendLine($"<th>Hit@{m.K}</th><th>P@{m.K}</th><th>R@{m.K}</th><th>RR</th><th>AP</th><th>NDCG@{m.K}</th><th>F1@{m.K}</th>");
            sb.AppendLine("</tr></thead><tbody><tr>");
            sb.AppendLine($"<td>{m.HitAtK:F4}</td><td>{m.PrecisionAtK:F4}</td><td>{m.RecallAtK:F4}</td>");
            sb.AppendLine($"<td>{m.ReciprocalRank:F4}</td><td>{m.AveragePrecision:F4}</td><td>{m.NdcgAtK:F4}</td><td>{m.F1AtK:F4}</td>");
            sb.AppendLine("</tr></tbody></table>");
        }

        sb.AppendLine("</details>");
    }

    private static void AggRow(StringBuilder sb, string name, double value)
    {
        var v = double.IsNaN(value) ? "N/A" : value.ToString("F4");
        sb.AppendLine($"<tr><td>{H(name)}</td><td>{v}</td></tr>");
    }

    // HTML-encode a string to prevent injection (chunk IDs, queries, etc. come from user data).
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Css() => """
        body{font-family:system-ui,sans-serif;max-width:1000px;margin:2rem auto;padding:0 1rem;color:#1a1a1a}
        header{border-bottom:2px solid #0066cc;padding-bottom:1rem;margin-bottom:2rem}
        h1{margin:0 0 .5rem;color:#0066cc}
        h2{margin-top:2rem;color:#003d7a}
        dl.meta{display:grid;grid-template-columns:auto 1fr;gap:.25rem 1rem;margin:0}
        dt{font-weight:600;color:#555}
        dd{margin:0}
        table{border-collapse:collapse;width:100%;margin-bottom:1rem}
        th,td{padding:.4rem .75rem;text-align:left;border:1px solid #ddd}
        thead{background:#f0f6ff}
        tr:nth-child(even){background:#fafafa}
        code{background:#f0f0f0;padding:.1em .3em;border-radius:3px;font-size:.9em}
        details.card{border:1px solid #ddd;border-radius:6px;margin-bottom:.75rem;padding:.5rem 1rem}
        details.card.error{border-color:#e00;background:#fff5f5}
        summary{cursor:pointer;font-size:1rem;padding:.25rem 0}
        .badge{font-size:.8em;background:#e8f0ff;border:1px solid #c0d0f0;border-radius:4px;padding:.1em .5em;margin-left:.5rem}
        .yes{color:#080;font-weight:700}
        .no{color:#888}
        .err{color:#c00;font-weight:600}
        table.metrics td{text-align:right}
        """;
}
