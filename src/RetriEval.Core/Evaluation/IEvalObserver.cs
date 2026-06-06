namespace RetriEval.Core;

/// <summary>
/// Receives lifecycle events from <see cref="EvalRunner"/>.
/// Use to show progress, pipe to telemetry, or log intermediate results.
/// Implementations must be thread-safe: events may fire from concurrent tasks.
/// </summary>
public interface IEvalObserver
{
    /// <summary>Called when a case is about to be evaluated.</summary>
    void OnCaseStarted(GoldenCase goldenCase);

    /// <summary>Called when a case has finished (successfully or with an error).</summary>
    void OnCaseCompleted(QueryResult result);

    /// <summary>Called once after all cases have been evaluated.</summary>
    void OnRunCompleted(EvalReport report);
}

/// <summary>No-op observer. Used when no observer is configured.</summary>
public sealed class NullEvalObserver : IEvalObserver
{
    /// <summary>Singleton instance.</summary>
    public static readonly NullEvalObserver Instance = new();

    /// <inheritdoc />
    public void OnCaseStarted(GoldenCase goldenCase) { }

    /// <inheritdoc />
    public void OnCaseCompleted(QueryResult result) { }

    /// <inheritdoc />
    public void OnRunCompleted(EvalReport report) { }
}

/// <summary>
/// Writes a one-line progress update to <see cref="Console.Out"/> for each completed case.
/// </summary>
public sealed class ConsoleEvalObserver : IEvalObserver
{
    /// <inheritdoc />
    public void OnCaseStarted(GoldenCase goldenCase)
        => Console.WriteLine($"  [EVAL] Starting: {goldenCase.Id}");

    /// <inheritdoc />
    public void OnCaseCompleted(QueryResult result)
    {
        var status = result.Error is not null ? "ERROR" : "OK";
        Console.WriteLine($"  [EVAL] {status}   : {result.CaseId}  Hit@k={result.Metrics.HitAtK:F3}");
    }

    /// <inheritdoc />
    public void OnRunCompleted(EvalReport report)
        => Console.WriteLine($"  [EVAL] Run complete. Cases={report.Results.Count}  MRR={report.Aggregate.Mrr:F3}");
}
