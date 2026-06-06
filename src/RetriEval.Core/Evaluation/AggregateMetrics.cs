namespace RetriEval.Core;

/// <summary>
/// Aggregate (mean) metric values across all non-errored golden cases in a run.
/// </summary>
public sealed record AggregateMetrics
{
    /// <summary>Mean Hit@k (proportion of queries that had at least one relevant result in top-k).</summary>
    public required double HitAtK { get; init; }

    /// <summary>Mean Precision@k.</summary>
    public required double MeanPrecisionAtK { get; init; }

    /// <summary>Mean Recall@k (known-item recall; see caveat in <see cref="RetrievalMetrics.RecallAtK"/>).</summary>
    public required double MeanRecallAtK { get; init; }

    /// <summary>Mean Reciprocal Rank.</summary>
    public required double Mrr { get; init; }

    /// <summary>Mean Average Precision.</summary>
    public required double Map { get; init; }

    /// <summary>Mean NDCG@k.</summary>
    public required double MeanNdcgAtK { get; init; }

    /// <summary>Mean F1@k.</summary>
    public required double MeanF1AtK { get; init; }

    /// <summary>The k value these aggregates were computed at.</summary>
    public required int K { get; init; }

    /// <summary>Number of cases included in the aggregate (excludes errored cases).</summary>
    public required int EvaluatedCaseCount { get; init; }

    /// <summary>Number of errored cases (excluded from aggregates).</summary>
    public required int ErroredCaseCount { get; init; }
}
