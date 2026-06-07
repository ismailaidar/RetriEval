namespace RetriEval.Core;

/// <summary>
/// An async-capable grader. Implement this instead of (or in addition to) <see cref="IGrader"/>
/// when relevance judgement involves I/O — most importantly when using an LLM as judge.
/// </summary>
/// <remarks>
/// <see cref="EvalRunner"/> checks whether the configured grader implements
/// <see cref="IAsyncGrader"/> and calls the async path when it does, avoiding the
/// sync-over-async pattern that would otherwise occur with LLM-backed graders.
/// <para>
/// Synchronous graders do not need to implement this interface.
/// </para>
/// </remarks>
public interface IAsyncGrader : IGrader
{
    /// <summary>
    /// Async variant of <see cref="IGrader.IsRelevant"/>.
    /// Called by <see cref="EvalRunner"/> in preference to the synchronous overload.
    /// </summary>
    Task<bool> IsRelevantAsync(RetrievedChunk chunk, GoldenCase @case, CancellationToken ct = default);

    /// <summary>
    /// Async variant of <see cref="IGrader.Gain"/>.
    /// Defaults to binary: 1 if relevant, 0 otherwise.
    /// Override when your grader can produce graded scores asynchronously.
    /// </summary>
    async Task<int> GainAsync(RetrievedChunk chunk, GoldenCase @case, CancellationToken ct = default)
        => await IsRelevantAsync(chunk, @case, ct).ConfigureAwait(false) ? 1 : 0;
}
