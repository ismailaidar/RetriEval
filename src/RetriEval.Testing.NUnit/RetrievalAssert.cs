using NUnit.Framework;
using RetriEval.Core;

namespace RetriEval.Testing.NUnit;

/// <summary>
/// NUnit assertion helpers for gating retrieval quality in CI.
/// Each method throws an NUnit <see cref="AssertionException"/> when the threshold is not met.
/// </summary>
/// <remarks>
/// Typical usage:
/// <code>
/// [Test]
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
        Assert.That(report.Aggregate.HitAtK, Is.GreaterThanOrEqualTo(threshold),
            $"RetriEval: Hit@{report.Aggregate.K}={report.Aggregate.HitAtK:F4} is below threshold {threshold:F4}.");
    }

    /// <summary>Asserts that the mean Precision@k is at least <paramref name="threshold"/>.</summary>
    public static void PrecisionAtK(EvalReport report, double threshold)
    {
        Guard(report);
        Assert.That(report.Aggregate.MeanPrecisionAtK, Is.GreaterThanOrEqualTo(threshold),
            $"RetriEval: Precision@{report.Aggregate.K}={report.Aggregate.MeanPrecisionAtK:F4} is below threshold {threshold:F4}.");
    }

    /// <summary>Asserts that the mean Recall@k is at least <paramref name="threshold"/>.</summary>
    public static void RecallAtK(EvalReport report, double threshold)
    {
        Guard(report);
        Assert.That(report.Aggregate.MeanRecallAtK, Is.GreaterThanOrEqualTo(threshold),
            $"RetriEval: Recall@{report.Aggregate.K}={report.Aggregate.MeanRecallAtK:F4} is below threshold {threshold:F4}.");
    }

    /// <summary>Asserts that the Mean Reciprocal Rank is at least <paramref name="threshold"/>.</summary>
    public static void Mrr(EvalReport report, double threshold)
    {
        Guard(report);
        Assert.That(report.Aggregate.Mrr, Is.GreaterThanOrEqualTo(threshold),
            $"RetriEval: MRR={report.Aggregate.Mrr:F4} is below threshold {threshold:F4}.");
    }

    /// <summary>Asserts that the Mean Average Precision is at least <paramref name="threshold"/>.</summary>
    public static void Map(EvalReport report, double threshold)
    {
        Guard(report);
        Assert.That(report.Aggregate.Map, Is.GreaterThanOrEqualTo(threshold),
            $"RetriEval: MAP={report.Aggregate.Map:F4} is below threshold {threshold:F4}.");
    }

    /// <summary>Asserts that the mean NDCG@k is at least <paramref name="threshold"/>.</summary>
    public static void NdcgAtK(EvalReport report, double threshold)
    {
        Guard(report);
        Assert.That(report.Aggregate.MeanNdcgAtK, Is.GreaterThanOrEqualTo(threshold),
            $"RetriEval: NDCG@{report.Aggregate.K}={report.Aggregate.MeanNdcgAtK:F4} is below threshold {threshold:F4}.");
    }

    /// <summary>Asserts that the mean F1@k is at least <paramref name="threshold"/>.</summary>
    public static void F1AtK(EvalReport report, double threshold)
    {
        Guard(report);
        Assert.That(report.Aggregate.MeanF1AtK, Is.GreaterThanOrEqualTo(threshold),
            $"RetriEval: F1@{report.Aggregate.K}={report.Aggregate.MeanF1AtK:F4} is below threshold {threshold:F4}.");
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
        Assert.Fail($"RetriEval: {errored.Count} case(s) errored during retrieval:\n  {details}");
    }

    /// <summary>
    /// Asserts that no regressions exist in the given <see cref="Reporting.RegressionReport"/>.
    /// </summary>
    public static void NoRegressions(Reporting.RegressionReport regressionReport)
    {
        ArgumentNullException.ThrowIfNull(regressionReport);
        if (!regressionReport.HasRegressions) return;
        var ids = string.Join(", ", regressionReport.RegressedCases.Select(c => c.CaseId));
        Assert.Fail($"RetriEval: {regressionReport.RegressedCases.Count} regressed case(s): {ids}");
    }

    /// <summary>
    /// Skips the test when <paramref name="report"/> is <see langword="null"/>.
    /// Use this when the retriever may not be configured in all environments.
    /// </summary>
    public static EvalReport RequireReport(EvalReport? report, string skipMessage = "Retriever not configured.")
    {
        if (report is not null) return report;
        Assert.Ignore(skipMessage);
        return null!; // unreachable; Assert.Ignore throws
    }

    private static void Guard(EvalReport report) => ArgumentNullException.ThrowIfNull(report);
}
