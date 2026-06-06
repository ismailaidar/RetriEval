namespace RetriEval.Core;

/// <summary>
/// Configuration options for <see cref="EvalRunner"/>.
/// </summary>
public sealed record EvalOptions
{
    /// <summary>Default number of results to retrieve per query.</summary>
    public int K { get; init; } = 3;

    /// <summary>Maximum number of golden cases evaluated concurrently.</summary>
    public int MaxConcurrency { get; init; } = 4;

    /// <summary>
    /// Number of times to retry a retriever call on transient failure before recording an error.
    /// Set to 0 to disable retries.
    /// </summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>
    /// Base delay for exponential backoff between retries.
    /// Effective delay for attempt n = <see cref="RetryBaseDelay"/> * 2^n.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Observer that receives lifecycle events during the run.
    /// Defaults to <see cref="NullEvalObserver"/> when <see langword="null"/>.
    /// </summary>
    public IEvalObserver? Observer { get; init; }
}
