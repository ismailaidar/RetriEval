using RetriEval.Core;

namespace RetriEval.Llm.Abstractions;

/// <summary>
/// Grades chunk relevance by asking an LLM to judge whether the chunk satisfies the query.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-deterministic and costly.</b> This grader calls an LLM for every (chunk, case) pair.
/// Results may differ across runs even with identical inputs. Use it for exploratory evaluation
/// or to bootstrap a golden set — not as the sole gate in reproducible CI. For CI, prefer
/// <see cref="ChunkIdGrader"/> or <see cref="KeywordGrader"/>.
/// </para>
/// <para>
/// The default prompt asks the model to respond with exactly "YES" or "NO". Override
/// <see cref="BuildPrompt"/> to customise the grading instruction.
/// </para>
/// </remarks>
public class LlmJudgeGrader : IGrader
{
    private readonly ILlmClient _llm;

    /// <param name="llm">LLM client used to evaluate relevance.</param>
    public LlmJudgeGrader(ILlmClient llm)
    {
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Calls <see cref="IsRelevantAsync"/> synchronously. Prefer <see cref="IsRelevantAsync"/>
    /// in async contexts to avoid sync-over-async issues.
    /// </remarks>
    public bool IsRelevant(RetrievedChunk chunk, GoldenCase @case) =>
        IsRelevantAsync(chunk, @case, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Async variant — sends one LLM request to judge whether <paramref name="chunk"/>
    /// is relevant to <paramref name="case"/>.
    /// </summary>
    public async Task<bool> IsRelevantAsync(RetrievedChunk chunk, GoldenCase @case,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(@case);

        var prompt = BuildPrompt(chunk, @case);
        var response = await _llm.CompleteAsync(prompt, ct).ConfigureAwait(false);
        return ParseYesNo(response);
    }

    /// <summary>
    /// Builds the relevance-judging prompt. Override to customise the instruction.
    /// The model must respond with "YES" or "NO" (case-insensitive, leading/trailing whitespace ignored).
    /// </summary>
    protected virtual string BuildPrompt(RetrievedChunk chunk, GoldenCase @case) =>
        $"""
        You are a retrieval evaluation judge. Decide whether the following chunk is relevant to the query.
        Respond with exactly YES or NO — nothing else.

        Query: {EscapeForPrompt(@case.Query)}

        Chunk content:
        {EscapeForPrompt(chunk.Content)}

        Is this chunk relevant to the query?
        """;

    private static bool ParseYesNo(string response)
    {
        var trimmed = response.Trim();
        return trimmed.StartsWith("YES", StringComparison.OrdinalIgnoreCase);
    }

    // Prevent prompt injection by stripping backticks and triple-dash delimiters.
    private static string EscapeForPrompt(string text) =>
        text.Replace("```", "'''").Replace("---", "- - -");
}
