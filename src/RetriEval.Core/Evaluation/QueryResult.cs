namespace RetriEval.Core;

/// <summary>
/// Evaluation result for a single golden case, including retrieved chunks, per-chunk relevance,
/// per-query metrics, and any error that occurred.
/// </summary>
public sealed record QueryResult
{
    /// <summary>ID of the golden case this result corresponds to.</summary>
    public required string CaseId { get; init; }

    /// <summary>The query string used.</summary>
    public required string Query { get; init; }

    /// <summary>Chunks returned by the retriever in rank order.</summary>
    public required IReadOnlyList<RetrievedChunk> Retrieved { get; init; }

    /// <summary>
    /// Per-chunk relevance flags in the same order as <see cref="Retrieved"/>.
    /// <see langword="true"/> means the chunk was judged relevant by the <see cref="IGrader"/>.
    /// </summary>
    public required IReadOnlyList<bool> Relevance { get; init; }

    /// <summary>Per-chunk gain values (used for NDCG).</summary>
    public required IReadOnlyList<int> Gains { get; init; }

    /// <summary>Computed metrics for this query.</summary>
    public required QueryMetrics Metrics { get; init; }

    /// <summary>
    /// Non-null when an exception was thrown during retrieval.
    /// Errored results are still included in the report; their metrics are zero.
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>Category from the golden case, for grouping.</summary>
    public string? Category { get; init; }
}
