namespace RetriEval.Core;

/// <summary>
/// A single chunk returned by a retriever, in descending relevance-rank order.
/// All properties are immutable; create via the primary constructor or <c>with</c> expressions.
/// </summary>
/// <param name="Id">Stable identifier that must uniquely address the chunk within the corpus.</param>
/// <param name="Content">The raw text of the chunk.</param>
/// <param name="Score">Retriever-assigned relevance score (higher = more relevant). Scale is retriever-specific.</param>
/// <param name="Metadata">Optional bag of string metadata (e.g. source document, page number).</param>
public sealed record RetrievedChunk(
    string Id,
    string Content,
    double Score,
    IReadOnlyDictionary<string, string>? Metadata = null);
