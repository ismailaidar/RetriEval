namespace RetriEval.Core;

/// <summary>
/// A single hand-labeled evaluation case pairing a query with its known-relevant chunks.
/// </summary>
/// <remarks>
/// Ground truth can be expressed three ways (combinable):
/// <list type="bullet">
///   <item><see cref="RelevantChunkIds"/> — strict id-based match. Most trustworthy; requires stable ids.</item>
///   <item><see cref="RelevantKeywords"/> — chunk content must contain at least one keyword. Survives re-chunking.</item>
///   <item><see cref="GradedRelevance"/> — per-chunk gain scores for NDCG; falls back to binary when absent.</item>
/// </list>
/// </remarks>
public sealed record GoldenCase
{
    /// <summary>Unique identifier for this evaluation case (used in reports and regression diffs).</summary>
    public required string Id { get; init; }

    /// <summary>The natural-language query sent to the retriever.</summary>
    public required string Query { get; init; }

    /// <summary>IDs of chunks considered relevant. An empty list means id-based matching is not used.</summary>
    public IReadOnlyList<string> RelevantChunkIds { get; init; } = [];

    /// <summary>
    /// Keywords that, when present in a chunk's content, mark that chunk as relevant.
    /// Matching is case-insensitive ordinal. An empty list means keyword matching is not used.
    /// </summary>
    public IReadOnlyList<string> RelevantKeywords { get; init; } = [];

    /// <summary>
    /// Optional per-chunk graded relevance (chunk id → gain value ≥ 0) for NDCG calculations.
    /// When <see langword="null"/>, binary gains (0 or 1) are used.
    /// </summary>
    public IReadOnlyDictionary<string, int>? GradedRelevance { get; init; }

    /// <summary>Optional category tag for grouping results in reports.</summary>
    public string? Category { get; init; }

    /// <summary>Optional human-readable description of what this case tests.</summary>
    public string? Description { get; init; }
}
