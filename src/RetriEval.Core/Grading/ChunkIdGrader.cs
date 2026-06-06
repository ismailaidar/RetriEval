namespace RetriEval.Core;

/// <summary>
/// Grades relevance by exact chunk-id membership.
/// </summary>
/// <remarks>
/// This is the strictest and most trustworthy grader — a chunk is relevant iff its
/// <see cref="RetrievedChunk.Id"/> appears in <see cref="GoldenCase.RelevantChunkIds"/>.
/// Requires stable, corpus-invariant chunk ids; re-chunking a document will invalidate the golden set.
/// When <see cref="GoldenCase.GradedRelevance"/> is provided, <see cref="Gain"/> returns the mapped value.
/// </remarks>
public sealed class ChunkIdGrader : IGrader
{
    /// <summary>Singleton instance — the grader is stateless.</summary>
    public static readonly ChunkIdGrader Instance = new();

    /// <inheritdoc />
    public bool IsRelevant(RetrievedChunk chunk, GoldenCase @case)
    {
        foreach (var id in @case.RelevantChunkIds)
        {
            if (string.Equals(id, chunk.Id, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    public int Gain(RetrievedChunk chunk, GoldenCase @case)
    {
        if (@case.GradedRelevance is { } graded && graded.TryGetValue(chunk.Id, out var gain))
            return gain;
        return IsRelevant(chunk, @case) ? 1 : 0;
    }
}
