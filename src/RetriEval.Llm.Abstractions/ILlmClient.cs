namespace RetriEval.Llm.Abstractions;

/// <summary>
/// Sends a prompt to a language model and returns the text completion.
/// Implement this against any LLM provider (Azure OpenAI, OpenAI, Ollama, etc.)
/// and pass an instance to <see cref="LlmJudgeGrader"/> or <see cref="GoldenSetGenerator"/>.
/// </summary>
/// <remarks>
/// <b>Cost and non-determinism:</b> Every call to <see cref="CompleteAsync"/> invokes an
/// external API that costs money and may produce different outputs across calls, even with the
/// same input. Components backed by <see cref="ILlmClient"/> are therefore non-deterministic
/// and unsuitable as the sole gate in a reproducible CI pipeline.
/// Use them to <em>augment</em> hand-labeled golden sets, never to replace them.
/// </remarks>
public interface ILlmClient
{
    /// <summary>
    /// Sends <paramref name="prompt"/> to the language model and returns the generated text.
    /// </summary>
    /// <param name="prompt">The full prompt string (system + user combined, or user-only).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
}
