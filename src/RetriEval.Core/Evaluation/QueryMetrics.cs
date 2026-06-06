namespace RetriEval.Core;

/// <summary>
/// All metric values computed for a single golden case.
/// </summary>
public sealed record QueryMetrics
{
    /// <summary>Hit@k: 1.0 if any relevant chunk appears in the top-k results, else 0.0.</summary>
    public required double HitAtK { get; init; }

    /// <summary>Precision at cut-off k.</summary>
    public required double PrecisionAtK { get; init; }

    /// <summary>
    /// Recall at cut-off k, measured against the hand-labeled relevant set.
    /// See <see cref="RetrievalMetrics.RecallAtK"/> for the known-item caveat.
    /// </summary>
    public required double RecallAtK { get; init; }

    /// <summary>Reciprocal Rank: 1/rank of first relevant result, or 0.</summary>
    public required double ReciprocalRank { get; init; }

    /// <summary>Average Precision.</summary>
    public required double AveragePrecision { get; init; }

    /// <summary>Normalized Discounted Cumulative Gain at cut-off k.</summary>
    public required double NdcgAtK { get; init; }

    /// <summary>Harmonic mean of Precision@k and Recall@k.</summary>
    public required double F1AtK { get; init; }

    /// <summary>The k value used for all cut-off metrics.</summary>
    public required int K { get; init; }
}
