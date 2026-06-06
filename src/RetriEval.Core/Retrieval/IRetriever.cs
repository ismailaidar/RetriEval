namespace RetriEval.Core;

/// <summary>
/// The system under test. Returns chunks ranked by descending relevance to the given query.
/// </summary>
/// <remarks>
/// Implement this interface once to plug any retrieval backend into the evaluation harness.
/// Implementations should be thread-safe: <see cref="EvalRunner"/> may call
/// <see cref="RetrieveAsync"/> concurrently across multiple golden cases.
/// </remarks>
public interface IRetriever
{
    /// <summary>
    /// Retrieves the top-<paramref name="k"/> chunks most relevant to <paramref name="query"/>.
    /// Results must be ordered by descending score (most relevant first).
    /// </summary>
    /// <param name="query">The natural-language query.</param>
    /// <param name="k">Maximum number of results to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A list of at most <paramref name="k"/> chunks in descending score order.
    /// May return fewer than <paramref name="k"/> items if the corpus is smaller.
    /// </returns>
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int k, CancellationToken ct = default);
}
