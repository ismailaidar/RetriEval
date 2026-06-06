using RetriEval.Core;
using Xunit;
using Xunit.Sdk; // XunitException

namespace RetriEval.Testing.Xunit;

/// <summary>
/// xUnit assertion helpers for gating retrieval quality in CI.
/// Each method throws an <see cref="XunitException"/> when the threshold is not met,
/// producing a descriptive failure message that names the metric and the measured value.
/// </summary>
/// <remarks>
/// Typical usage — add one test per retrieval quality gate:
/// <code>
/// [Fact]
/// public async Task Retrieval_MeetsQualityBar()
/// {
///     var report = await _runner.RunAsync(_goldenSet);
///     RetrievalAssert.HitAtK(report, threshold: 0.80);
///     RetrievalAssert.Mrr(report, threshold: 0.60);
///     RetrievalAssert.NoErrors(report);
/// }
/// </code>
/// </remarks>
public static class RetrievalAssert
{
    /// <summary>Asserts that the mean Hit@k is at least <paramref name="threshold"/>.</summary>
    public static void HitAtK(EvalReport report, double threshold)
    {
        Guard(report);
        var value = report.Aggregate.HitAtK;
        if (value < threshold)
            throw Fail($"Hit@{report.Aggregate.K}", value, threshold);
    }

    /// <summary>Asserts that the mean Precision@k is at least <paramref name="threshold"/>.</summary>
    public static void PrecisionAtK(EvalReport report, double threshold)
    {
        Guard(report);
        var value = report.Aggregate.MeanPrecisionAtK;
        if (value < threshold)
            throw Fail($"Precision@{report.Aggregate.K}", value, threshold);
    }

    /// <summary>Asserts that the mean Recall@k is at least <paramref name="threshold"/>.</summary>
    public static void RecallAtK(EvalReport report, double threshold)
    {
        Guard(report);
        var value = report.Aggregate.MeanRecallAtK;
        if (value < threshold)
            throw Fail($"Recall@{report.Aggregate.K}", value, threshold);
    }

    /// <summary>Asserts that the Mean Reciprocal Rank is at least <paramref name="threshold"/>.</summary>
    public static void Mrr(EvalReport report, double threshold)
    {
        Guard(report);
        var value = report.Aggregate.Mrr;
        if (value < threshold)
            throw Fail("MRR", value, threshold);
    }

    /// <summary>Asserts that the Mean Average Precision is at least <paramref name="threshold"/>.</summary>
    public static void Map(EvalReport report, double threshold)
    {
        Guard(report);
        var value = report.Aggregate.Map;
        if (value < threshold)
            throw Fail("MAP", value, threshold);
    }

    /// <summary>Asserts that the mean NDCG@k is at least <paramref name="threshold"/>.</summary>
    public static void NdcgAtK(EvalReport report, double threshold)
    {
        Guard(report);
        var value = report.Aggregate.MeanNdcgAtK;
        if (value < threshold)
            throw Fail($"NDCG@{report.Aggregate.K}", value, threshold);
    }

    /// <summary>Asserts that the mean F1@k is at least <paramref name="threshold"/>.</summary>
    public static void F1AtK(EvalReport report, double threshold)
    {
        Guard(report);
        var value = report.Aggregate.MeanF1AtK;
        if (value < threshold)
            throw Fail($"F1@{report.Aggregate.K}", value, threshold);
    }

    /// <summary>
    /// Asserts that no golden cases errored during the run.
    /// Fails with a list of errored case ids and their error messages.
    /// </summary>
    public static void NoErrors(EvalReport report)
    {
        Guard(report);
        var errored = report.Results.Where(r => r.Error is not null).ToList();
        if (errored.Count == 0) return;

        var details = string.Join("\n  ", errored.Select(r => $"[{r.CaseId}]: {r.Error!.Message}"));
        throw new XunitException(
            $"RetriEval: {errored.Count} case(s) errored during retrieval:\n  {details}");
    }

    /// <summary>
    /// Asserts that no regressions exist in the given <see cref="Reporting.RegressionReport"/>.
    /// </summary>
    public static void NoRegressions(Reporting.RegressionReport regressionReport)
    {
        ArgumentNullException.ThrowIfNull(regressionReport);
        if (!regressionReport.HasRegressions) return;

        var ids = string.Join(", ", regressionReport.RegressedCases.Select(c => c.CaseId));
        throw new XunitException(
            $"RetriEval: {regressionReport.RegressedCases.Count} regressed case(s): {ids}");
    }

    /// <summary>
    /// Returns <paramref name="report"/> when non-null, or throws when null.
    /// Use to guard tests that require a live retriever:
    /// <code>
    /// var report = RetrievalAssert.RequireReport(maybeNullReport);
    /// </code>
    /// To skip rather than fail when the retriever is absent, guard with
    /// <c>Skip.If(report is null, "Retriever not configured.")</c>
    /// from the <c>xunit</c> package before calling this method.
    /// </summary>
    public static EvalReport RequireReport(EvalReport? report, string failMessage = "Retriever not configured.")
    {
        if (report is not null) return report;
        throw new XunitException($"RetriEval: {failMessage}");
    }

    private static void Guard(EvalReport report) => ArgumentNullException.ThrowIfNull(report);

    private static XunitException Fail(string metric, double value, double threshold) =>
        new($"RetriEval: {metric}={value:F4} is below threshold {threshold:F4}.");
}
