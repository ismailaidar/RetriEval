using RetriEval.Core;

namespace RetriEval.Reporting;

/// <summary>
/// Diffs two <see cref="EvalReport"/> runs to surface regressions, improvements,
/// and per-query deltas. This is the core of the "track quality over time" workflow.
/// </summary>
public sealed class RegressionComparer
{
    /// <summary>
    /// Compares <paramref name="baseline"/> against <paramref name="current"/> and returns a
    /// <see cref="RegressionReport"/> describing what changed.
    /// </summary>
    /// <param name="baseline">The reference run (e.g. the main-branch result).</param>
    /// <param name="current">The run under review (e.g. a PR branch result).</param>
    public RegressionReport Compare(EvalReport baseline, EvalReport current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        var baselineById = baseline.Results.ToDictionary(r => r.CaseId);
        var currentById  = current.Results.ToDictionary(r => r.CaseId);

        var queryDeltas = new List<QueryDelta>();

        foreach (var (id, cur) in currentById)
        {
            if (!baselineById.TryGetValue(id, out var bas))
            {
                queryDeltas.Add(new QueryDelta { CaseId = id, Status = DeltaStatus.Added });
                continue;
            }

            if (bas.Error is not null || cur.Error is not null)
            {
                queryDeltas.Add(new QueryDelta
                {
                    CaseId = id,
                    Status = cur.Error is not null ? DeltaStatus.Errored : DeltaStatus.Fixed,
                    BaselineError = bas.Error?.Message,
                    CurrentError  = cur.Error?.Message,
                });
                continue;
            }

            var delta = ComputeMetricDelta(bas.Metrics, cur.Metrics);
            queryDeltas.Add(new QueryDelta
            {
                CaseId = id,
                Query  = cur.Query,
                Status = ClassifyDelta(delta),
                MetricDelta = delta,
            });
        }

        foreach (var id in baselineById.Keys.Except(currentById.Keys))
            queryDeltas.Add(new QueryDelta { CaseId = id, Status = DeltaStatus.Removed });

        var aggregateDelta = ComputeAggregateDelta(baseline.Aggregate, current.Aggregate);

        return new RegressionReport
        {
            BaselineRunAt    = baseline.RunAt,
            CurrentRunAt     = current.RunAt,
            BaselineRetriever = baseline.RetrieverName,
            CurrentRetriever  = current.RetrieverName,
            AggregateDelta   = aggregateDelta,
            QueryDeltas      = queryDeltas,
            RegressedCases   = queryDeltas.Where(d => d.Status == DeltaStatus.Regressed).ToList(),
            ImprovedCases    = queryDeltas.Where(d => d.Status == DeltaStatus.Improved).ToList(),
        };
    }

    private static MetricDelta ComputeMetricDelta(QueryMetrics baseline, QueryMetrics current) => new()
    {
        HitAtK          = current.HitAtK          - baseline.HitAtK,
        PrecisionAtK     = current.PrecisionAtK     - baseline.PrecisionAtK,
        RecallAtK        = current.RecallAtK        - baseline.RecallAtK,
        ReciprocalRank   = current.ReciprocalRank   - baseline.ReciprocalRank,
        AveragePrecision = current.AveragePrecision - baseline.AveragePrecision,
        NdcgAtK          = current.NdcgAtK          - baseline.NdcgAtK,
        F1AtK            = current.F1AtK            - baseline.F1AtK,
    };

    private static AggregateMetricDelta ComputeAggregateDelta(AggregateMetrics baseline, AggregateMetrics current) => new()
    {
        HitAtK          = current.HitAtK          - baseline.HitAtK,
        MeanPrecisionAtK = current.MeanPrecisionAtK - baseline.MeanPrecisionAtK,
        MeanRecallAtK    = current.MeanRecallAtK    - baseline.MeanRecallAtK,
        Mrr              = current.Mrr              - baseline.Mrr,
        Map              = current.Map              - baseline.Map,
        MeanNdcgAtK      = current.MeanNdcgAtK      - baseline.MeanNdcgAtK,
        MeanF1AtK        = current.MeanF1AtK        - baseline.MeanF1AtK,
    };

    private static DeltaStatus ClassifyDelta(MetricDelta delta)
    {
        // Primary signal: NDCG is the most holistic metric; RR as tie-breaker.
        if (delta.NdcgAtK < -0.001 || delta.ReciprocalRank < -0.001)
            return DeltaStatus.Regressed;
        if (delta.NdcgAtK > 0.001 || delta.ReciprocalRank > 0.001)
            return DeltaStatus.Improved;
        return DeltaStatus.Unchanged;
    }
}

/// <summary>Full regression comparison between two runs.</summary>
public sealed record RegressionReport
{
    /// <inheritdoc cref="EvalReport.RunAt"/> for the baseline run.
    public required DateTimeOffset BaselineRunAt { get; init; }

    /// <inheritdoc cref="EvalReport.RunAt"/> for the current run.
    public required DateTimeOffset CurrentRunAt { get; init; }

    /// <inheritdoc cref="EvalReport.RetrieverName"/> for the baseline run.
    public required string BaselineRetriever { get; init; }

    /// <inheritdoc cref="EvalReport.RetrieverName"/> for the current run.
    public required string CurrentRetriever { get; init; }

    /// <summary>Aggregate metric deltas (current − baseline).</summary>
    public required AggregateMetricDelta AggregateDelta { get; init; }

    /// <summary>All per-query deltas.</summary>
    public required IReadOnlyList<QueryDelta> QueryDeltas { get; init; }

    /// <summary>Cases where quality dropped (NDCG or RR decreased by more than 0.001).</summary>
    public required IReadOnlyList<QueryDelta> RegressedCases { get; init; }

    /// <summary>Cases where quality improved.</summary>
    public required IReadOnlyList<QueryDelta> ImprovedCases { get; init; }

    /// <summary>Whether any case regressed.</summary>
    public bool HasRegressions => RegressedCases.Count > 0;
}

/// <summary>Per-query delta between two runs.</summary>
public sealed record QueryDelta
{
    /// <inheritdoc cref="QueryResult.CaseId"/>
    public required string CaseId { get; init; }

    /// <inheritdoc cref="QueryResult.Query"/>
    public string? Query { get; init; }

    /// <summary>Classification of what changed for this case.</summary>
    public required DeltaStatus Status { get; init; }

    /// <summary>Per-metric delta values. <see langword="null"/> for added/removed/errored cases.</summary>
    public MetricDelta? MetricDelta { get; init; }

    /// <summary>Baseline error message (when status is <see cref="DeltaStatus.Fixed"/>).</summary>
    public string? BaselineError { get; init; }

    /// <summary>Current error message (when status is <see cref="DeltaStatus.Errored"/>).</summary>
    public string? CurrentError { get; init; }
}

/// <summary>Classification of how a case changed between two runs.</summary>
public enum DeltaStatus
{
    /// <summary>Metrics are essentially unchanged (within tolerance).</summary>
    Unchanged,
    /// <summary>Quality improved.</summary>
    Improved,
    /// <summary>Quality regressed.</summary>
    Regressed,
    /// <summary>Case exists in current but not in baseline.</summary>
    Added,
    /// <summary>Case exists in baseline but not in current.</summary>
    Removed,
    /// <summary>Case errored in current.</summary>
    Errored,
    /// <summary>Case previously errored in baseline but succeeded in current.</summary>
    Fixed,
}

/// <summary>Aggregate metric deltas (current − baseline).</summary>
public sealed record AggregateMetricDelta
{
    /// <summary>Change in Hit@k.</summary>
    public required double HitAtK { get; init; }
    /// <summary>Change in mean Precision@k.</summary>
    public required double MeanPrecisionAtK { get; init; }
    /// <summary>Change in mean Recall@k.</summary>
    public required double MeanRecallAtK { get; init; }
    /// <summary>Change in MRR.</summary>
    public required double Mrr { get; init; }
    /// <summary>Change in MAP.</summary>
    public required double Map { get; init; }
    /// <summary>Change in mean NDCG@k.</summary>
    public required double MeanNdcgAtK { get; init; }
    /// <summary>Change in mean F1@k.</summary>
    public required double MeanF1AtK { get; init; }
}

/// <summary>Per-query metric deltas (current − baseline).</summary>
public sealed record MetricDelta
{
    /// <summary>Change in Hit@k.</summary>
    public required double HitAtK { get; init; }
    /// <summary>Change in Precision@k.</summary>
    public required double PrecisionAtK { get; init; }
    /// <summary>Change in Recall@k.</summary>
    public required double RecallAtK { get; init; }
    /// <summary>Change in Reciprocal Rank.</summary>
    public required double ReciprocalRank { get; init; }
    /// <summary>Change in Average Precision.</summary>
    public required double AveragePrecision { get; init; }
    /// <summary>Change in NDCG@k.</summary>
    public required double NdcgAtK { get; init; }
    /// <summary>Change in F1@k.</summary>
    public required double F1AtK { get; init; }
}
