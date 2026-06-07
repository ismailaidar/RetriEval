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
/// <para>
/// <b>Warning — keyword presence ≠ query relevance.</b>
/// Consider a query "Does Humira require prior authorization?" with keyword "Humira".
/// A chunk containing "Store Humira at 36–46 °F" would be graded relevant even though it does not
/// answer the prior-authorization question. This false-positive problem grows with common or
/// brand-name terms that appear in many unrelated chunks.
/// For stronger relevance signals, use <see cref="ChunkIdGrader"/> (when IDs are stable)
/// or <c>LlmJudgeGrader</c> (RetriEval.Llm.Abstractions) for semantic judgement.
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
