namespace RetriEval.Core;

/// <summary>
/// Decides whether a retrieved chunk satisfies a golden case.
/// Implement this to plug in custom relevance logic (e.g. semantic similarity, LLM judge).
/// </summary>
/// <remarks>
/// Implementations must be pure and thread-safe — <see cref="EvalRunner"/> may call
/// them concurrently across multiple cases.
/// </remarks>
public interface IGrader
{
    /// <summary>Returns <see langword="true"/> when <paramref name="chunk"/> is relevant to <paramref name="case"/>.</summary>
    bool IsRelevant(RetrievedChunk chunk, GoldenCase @case);

    /// <summary>
    /// Returns the relevance gain for <paramref name="chunk"/> (used by NDCG).
    /// Defaults to binary: 1 if relevant, 0 otherwise.
    /// Override to supply graded gains from <see cref="GoldenCase.GradedRelevance"/>.
    /// </summary>
    int Gain(RetrievedChunk chunk, GoldenCase @case) => IsRelevant(chunk, @case) ? 1 : 0;
}
