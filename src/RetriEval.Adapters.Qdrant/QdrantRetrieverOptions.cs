namespace RetriEval.Adapters.Qdrant;

/// <summary>
/// Configuration for <see cref="QdrantRetriever"/>.
/// </summary>
public sealed record QdrantRetrieverOptions
{
    /// <summary>
    /// Name of the Qdrant collection to query.
    /// </summary>
    public required string CollectionName { get; init; }

    /// <summary>
    /// Payload field that contains the chunk identifier.
    /// Defaults to <c>"id"</c>.
    /// </summary>
    public string IdField { get; init; } = "id";

    /// <summary>
    /// Payload field that contains the chunk text.
    /// Defaults to <c>"content"</c>.
    /// </summary>
    public string ContentField { get; init; } = "content";

    /// <summary>
    /// Additional payload fields to include as <see cref="RetriEval.Core.RetrievedChunk.Metadata"/>.
    /// </summary>
    public IReadOnlyList<string> MetadataFields { get; init; } = [];

    /// <summary>
    /// Name of the named vector to use for search.
    /// Set to <see langword="null"/> to use the default (unnamed) vector.
    /// </summary>
    public string? VectorName { get; init; }

    /// <summary>
    /// Converts the query string to a dense vector used for nearest-neighbour search.
    /// Required — Qdrant is a vector store and always requires a query vector.
    /// </summary>
    public required Func<string, CancellationToken, Task<float[]>> QueryEmbedder { get; init; }

    /// <summary>
    /// Optional score threshold. Results with a score below this value are excluded.
    /// <see langword="null"/> disables score filtering.
    /// </summary>
    public float? ScoreThreshold { get; init; }
}
