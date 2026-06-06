namespace RetriEval.Core;

/// <summary>
/// A deterministic in-memory retriever for tests and samples.
/// Scores chunks by counting how many query words appear in the content (TF-style),
/// breaking ties by insertion order. No external dependencies, no randomness.
/// </summary>
/// <remarks>
/// This is not a production retriever — it is intentionally simple so that samples and
/// unit tests run without any external services. Use a real adapter (AzureAISearch, Qdrant, …)
/// for production evaluation.
/// </remarks>
public sealed class InMemoryRetriever : IRetriever
{
    private readonly IReadOnlyList<RetrievedChunk> _corpus;

    /// <param name="chunks">The full set of chunks to search over. Order determines tie-breaking.</param>
    public InMemoryRetriever(IEnumerable<RetrievedChunk> chunks)
    {
        _corpus = [.. chunks];
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int k, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Score = number of query words found in content (case-insensitive). Stable sort on corpus index.
        var scored = _corpus
            .Select((chunk, index) => (chunk, score: Score(chunk.Content, words), index))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Take(k)
            .Select(x => x.chunk with { Score = x.score })
            .ToList();

        return Task.FromResult<IReadOnlyList<RetrievedChunk>>(scored);
    }

    private static double Score(string content, string[] words)
    {
        double hits = 0;
        foreach (var word in words)
        {
            if (content.Contains(word, StringComparison.OrdinalIgnoreCase))
                hits++;
        }
        return hits;
    }
}
