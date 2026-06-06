namespace RetriEval.Core;

/// <summary>
/// The full output of an evaluation run: aggregate metrics, per-query results, and run metadata.
/// </summary>
public sealed record EvalReport
{
    /// <summary>UTC timestamp when the run started.</summary>
    public required DateTimeOffset RunAt { get; init; }

    /// <summary>Aggregate (mean) metrics across all non-errored cases.</summary>
    public required AggregateMetrics Aggregate { get; init; }

    /// <summary>Per-query results in the order the golden cases were supplied.</summary>
    public required IReadOnlyList<QueryResult> Results { get; init; }

    /// <summary>Options the run was configured with.</summary>
    public required EvalOptions Options { get; init; }

    /// <summary>
    /// Name of the retriever, used in reports and diffs.
    /// Derived from <see cref="IRetriever"/> type name by default.
    /// </summary>
    public required string RetrieverName { get; init; }

    /// <summary>
    /// Name of the grader, used in reports and diffs.
    /// Derived from <see cref="IGrader"/> type name by default.
    /// </summary>
    public required string GraderName { get; init; }

    /// <summary>Convenience: results where <see cref="QueryResult.Error"/> is non-null.</summary>
    public IEnumerable<QueryResult> ErroredResults => Results.Where(r => r.Error is not null);
}
