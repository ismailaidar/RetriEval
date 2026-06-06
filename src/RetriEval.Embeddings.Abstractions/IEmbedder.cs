namespace RetriEval.Embeddings.Abstractions;

/// <summary>
/// Converts text to a dense embedding vector.
/// Implement this against any embedding provider (Azure OpenAI, OpenAI, Ollama, etc.)
/// and pass an instance to <see cref="SemanticGrader"/>.
/// </summary>
/// <remarks>
/// Implementations must be thread-safe — <see cref="SemanticGrader"/> may call
/// <see cref="EmbedAsync"/> concurrently from multiple evaluation tasks.
/// </remarks>
public interface IEmbedder
{
    /// <summary>
    /// Returns the embedding vector for <paramref name="text"/>.
    /// The vector length must be consistent across all calls from the same instance.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
