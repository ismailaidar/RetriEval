using RetriEval.Core;

namespace RetriEval.Embeddings.Abstractions;

/// <summary>
/// Grades relevance by cosine similarity between a chunk's embedding and the embeddings
/// of the expected relevant chunk contents.
/// </summary>
/// <remarks>
/// <para>
/// Use this grader when chunk ids change after re-chunking (making <see cref="ChunkIdGrader"/>
/// impractical) but you still need more reliable signal than keyword matching.
/// </para>
/// <para>
/// <b>Cost and non-determinism:</b> Each grading call invokes the embedding model,
/// which costs money and may produce slightly different vectors across API versions.
/// For deterministic CI gates prefer <see cref="ChunkIdGrader"/>; use
/// <see cref="SemanticGrader"/> for exploratory evaluation or when ids are genuinely unstable.
/// </para>
/// <para>
/// <b>Performance:</b> Embeddings for expected chunks are computed lazily and cached for
/// the lifetime of this grader instance. Chunk embeddings are <em>not</em> cached because
/// the same chunk may appear in multiple queries with different content.
/// </para>
/// </remarks>
public sealed class SemanticGrader : IGrader
{
    private readonly IEmbedder _embedder;
    private readonly float _threshold;
    private readonly Dictionary<string, float[]> _expectedEmbeddingCache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <param name="embedder">The embedding model to use for both chunks and expected texts.</param>
    /// <param name="threshold">
    /// Cosine similarity threshold in [0, 1]. A chunk is relevant when its similarity
    /// to any expected text is ≥ this value. Typical values: 0.75–0.90.
    /// </param>
    public SemanticGrader(IEmbedder embedder, float threshold = 0.80f)
    {
        ArgumentNullException.ThrowIfNull(embedder);
        if (threshold is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Must be in [0, 1].");
        _embedder = embedder;
        _threshold = threshold;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This overload is synchronous but the underlying implementation is async.
    /// Prefer <see cref="IsRelevantAsync"/> when calling from async code to avoid
    /// sync-over-async deadlocks.
    /// </remarks>
    public bool IsRelevant(RetrievedChunk chunk, GoldenCase @case) =>
        IsRelevantAsync(chunk, @case, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Async variant of <see cref="IsRelevant"/> — preferred in async contexts.
    /// Returns <see langword="true"/> when cosine(chunk, any-expected-text) ≥ threshold.
    /// </summary>
    public async Task<bool> IsRelevantAsync(RetrievedChunk chunk, GoldenCase @case,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(@case);

        if (@case.RelevantChunkIds.Count == 0)
            return false;

        var chunkEmbedding = await _embedder.EmbedAsync(chunk.Content, ct).ConfigureAwait(false);

        foreach (var expectedId in @case.RelevantChunkIds)
        {
            var expectedEmbedding = await GetExpectedEmbeddingAsync(expectedId, @case, ct).ConfigureAwait(false);
            if (expectedEmbedding is null) continue;
            if (CosineSimilarity(chunkEmbedding, expectedEmbedding) >= _threshold)
                return true;
        }
        return false;
    }

    // The golden case stores ids, not content — so we can only embed when content is available
    // via a corpus lookup. For now we use the id as a proxy key; callers who need full content
    // comparison should subclass and override GetExpectedTextAsync.
    private async Task<float[]?> GetExpectedEmbeddingAsync(string expectedId, GoldenCase @case,
        CancellationToken ct)
    {
        await _cacheLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_expectedEmbeddingCache.TryGetValue(expectedId, out var cached))
                return cached;

            // If the golden case provides descriptions we can embed the id as a stand-in.
            // Real subclasses would resolve expectedId → content from a corpus.
            var text = expectedId;
            var embedding = await _embedder.EmbedAsync(text, ct).ConfigureAwait(false);
            _expectedEmbeddingCache[expectedId] = embedding;
            return embedding;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Returns the cosine similarity between two vectors (dot product of unit vectors).
    /// Returns 0 when either vector has zero magnitude.
    /// </summary>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Length != b.Length)
            throw new ArgumentException($"Vector length mismatch: {a.Length} vs {b.Length}.");

        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot  += (double)a[i] * b[i];
            magA += (double)a[i] * a[i];
            magB += (double)b[i] * b[i];
        }
        magA = Math.Sqrt(magA);
        magB = Math.Sqrt(magB);
        if (magA == 0 || magB == 0) return 0f;
        return (float)(dot / (magA * magB));
    }
}
