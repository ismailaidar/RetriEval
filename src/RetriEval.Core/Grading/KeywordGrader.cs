namespace RetriEval.Core;

/// <summary>
/// Grades relevance by keyword presence in chunk content.
/// </summary>
/// <remarks>
/// A chunk is relevant iff its <see cref="RetrievedChunk.Content"/> contains at least one keyword
/// from <see cref="GoldenCase.RelevantKeywords"/>. Matching is case-insensitive ordinal.
/// <para>
/// This grader is more resilient to re-chunking than <see cref="ChunkIdGrader"/> but is fuzzier —
/// a keyword match does not guarantee semantic relevance. Use when chunk ids are unstable or absent.
/// </para>
/// </remarks>
public sealed class KeywordGrader : IGrader
{
    /// <summary>Singleton instance — the grader is stateless.</summary>
    public static readonly KeywordGrader Instance = new();

    /// <inheritdoc />
    public bool IsRelevant(RetrievedChunk chunk, GoldenCase @case)
    {
        foreach (var keyword in @case.RelevantKeywords)
        {
            if (chunk.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
