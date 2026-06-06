namespace RetriEval.Adapters.AzureAISearch;

/// <summary>
/// Configuration for <see cref="AzureAISearchRetriever"/>.
/// Maps Azure AI Search field names to the <see cref="RetriEval.Core.RetrievedChunk"/> properties.
/// </summary>
public sealed record AzureAISearchRetrieverOptions
{
    /// <summary>
    /// Name of the search index to query.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Field in the search index that contains the document/chunk identifier.
    /// Defaults to <c>"id"</c>.
    /// </summary>
    public string IdField { get; init; } = "id";

    /// <summary>
    /// Field in the search index that contains the chunk text.
    /// Defaults to <c>"content"</c>.
    /// </summary>
    public string ContentField { get; init; } = "content";

    /// <summary>
    /// Additional fields to include as <see cref="RetriEval.Core.RetrievedChunk.Metadata"/>.
    /// When empty, no metadata is attached. Defaults to an empty list.
    /// </summary>
    public IReadOnlyList<string> MetadataFields { get; init; } = [];

    /// <summary>
    /// The name of the vector field used for vector/hybrid search.
    /// Set to <see langword="null"/> to use keyword-only search.
    /// Defaults to <c>"contentVector"</c>.
    /// </summary>
    public string? VectorField { get; init; } = "contentVector";

    /// <summary>
    /// Embedding provider used to vectorize the query for vector/hybrid search.
    /// Required when <see cref="VectorField"/> is set.
    /// </summary>
    public Func<string, CancellationToken, Task<float[]>>? QueryEmbedder { get; init; }

    /// <summary>
    /// When <see langword="true"/>, uses hybrid search (keyword + vector). Requires both
    /// <see cref="VectorField"/> and <see cref="QueryEmbedder"/> to be configured.
    /// When <see langword="false"/> (default), uses pure vector search when a vector field is
    /// configured, or pure keyword search otherwise.
    /// </summary>
    public bool UseHybridSearch { get; init; } = false;
}
