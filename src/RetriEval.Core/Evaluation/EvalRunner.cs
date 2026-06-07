namespace RetriEval.Core;

/// <summary>
/// Orchestrates an evaluation run: retrieves results for each golden case concurrently,
/// grades them, computes per-query and aggregate metrics, and returns an <see cref="EvalReport"/>.
/// </summary>
public sealed class EvalRunner
{
    private readonly IRetriever _retriever;
    private readonly IGrader _grader;
    private readonly EvalOptions _options;

    /// <param name="retriever">The system under test.</param>
    /// <param name="grader">Relevance judge applied to each retrieved chunk.</param>
    /// <param name="options">Run configuration. Defaults are applied when <see langword="null"/>.</param>
    public EvalRunner(IRetriever retriever, IGrader grader, EvalOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(retriever);
        ArgumentNullException.ThrowIfNull(grader);
        _retriever = retriever;
        _grader = grader;
        _options = options ?? new EvalOptions();
    }

    /// <summary>
    /// Evaluates every case in <paramref name="goldenSet"/> and returns the aggregate report.
    /// Cases run concurrently up to <see cref="EvalOptions.MaxConcurrency"/>.
    /// Retriever failures are captured per-case and do not abort the run.
    /// </summary>
    /// <param name="goldenSet">The labeled evaluation cases to run.</param>
    /// <param name="ct">Cancellation token. Cancelling stops new cases from starting.</param>
    public async Task<EvalReport> RunAsync(IEnumerable<GoldenCase> goldenSet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goldenSet);

        var observer = _options.Observer ?? NullEvalObserver.Instance;
        var runAt = DateTimeOffset.UtcNow;
        var cases = goldenSet.ToList();
        var results = new QueryResult[cases.Count];

        var sem = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, cases.Count),
            new ParallelOptions { MaxDegreeOfParallelism = _options.MaxConcurrency, CancellationToken = ct },
            async (i, token) =>
            {
                var goldenCase = cases[i];
                observer.OnCaseStarted(goldenCase);
                var result = await EvaluateCaseAsync(goldenCase, token).ConfigureAwait(false);
                results[i] = result;
                observer.OnCaseCompleted(result);
            }).ConfigureAwait(false);

        var report = BuildReport(runAt, results, cases.Count);
        observer.OnRunCompleted(report);
        return report;
    }

    private async Task<QueryResult> EvaluateCaseAsync(GoldenCase goldenCase, CancellationToken ct)
    {
        var k = _options.K;

        IReadOnlyList<RetrievedChunk> retrieved;
        try
        {
            retrieved = await RetrieveWithRetryAsync(goldenCase.Query, k, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorResult(goldenCase, k, ex);
        }

        var relevance = new bool[retrieved.Count];
        var gains = new int[retrieved.Count];

        // Prefer the async path to avoid sync-over-async with LLM-backed graders.
        if (_grader is IAsyncGrader asyncGrader)
        {
            for (var i = 0; i < retrieved.Count; i++)
            {
                relevance[i] = await asyncGrader.IsRelevantAsync(retrieved[i], goldenCase, ct).ConfigureAwait(false);
                gains[i]     = await asyncGrader.GainAsync(retrieved[i], goldenCase, ct).ConfigureAwait(false);
            }
        }
        else
        {
            for (var i = 0; i < retrieved.Count; i++)
            {
                relevance[i] = _grader.IsRelevant(retrieved[i], goldenCase);
                gains[i]     = _grader.Gain(retrieved[i], goldenCase);
            }
        }

        var totalRelevant = Math.Max(1, goldenCase.RelevantChunkIds.Count + goldenCase.RelevantKeywords.Count > 0
            ? CountTotalRelevant(goldenCase)
            : relevance.Count(r => r));

        var metrics = new QueryMetrics
        {
            K = k,
            HitAtK = RetrievalMetrics.HitAtK(relevance, k),
            PrecisionAtK = RetrievalMetrics.PrecisionAtK(relevance, k),
            RecallAtK = RetrievalMetrics.RecallAtK(relevance, k, totalRelevant),
            ReciprocalRank = RetrievalMetrics.ReciprocalRank(relevance),
            AveragePrecision = RetrievalMetrics.AveragePrecision(relevance),
            NdcgAtK = RetrievalMetrics.NdcgAtK(gains, k),
            F1AtK = RetrievalMetrics.F1AtK(relevance, k, totalRelevant),
        };

        return new QueryResult
        {
            CaseId = goldenCase.Id,
            Query = goldenCase.Query,
            Retrieved = retrieved,
            Relevance = relevance,
            Gains = gains,
            Metrics = metrics,
            Category = goldenCase.Category,
        };
    }

    private async Task<IReadOnlyList<RetrievedChunk>> RetrieveWithRetryAsync(
        string query, int k, CancellationToken ct)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await _retriever.RetrieveAsync(query, k, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < _options.MaxRetries)
            {
                attempt++;
                var delay = _options.RetryBaseDelay * Math.Pow(2, attempt - 1);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    private static int CountTotalRelevant(GoldenCase goldenCase)
    {
        // For id-based grading, the relevant set size is the count of labeled relevant ids.
        // For keyword grading, we cannot know corpus-wide relevant count — use the labeled id count as a proxy,
        // or fall back to 1 so Recall stays in [0,1].
        return Math.Max(1, goldenCase.RelevantChunkIds.Count);
    }

    private static QueryResult ErrorResult(GoldenCase goldenCase, int k, Exception ex)
    {
        var zeroMetrics = new QueryMetrics
        {
            K = k,
            HitAtK = 0, PrecisionAtK = 0, RecallAtK = 0,
            ReciprocalRank = 0, AveragePrecision = 0, NdcgAtK = 0, F1AtK = 0,
        };
        return new QueryResult
        {
            CaseId = goldenCase.Id,
            Query = goldenCase.Query,
            Retrieved = [],
            Relevance = [],
            Gains = [],
            Metrics = zeroMetrics,
            Error = ex,
            Category = goldenCase.Category,
        };
    }

    private EvalReport BuildReport(DateTimeOffset runAt, QueryResult[] results, int totalCases)
    {
        var ok = results.Where(r => r.Error is null).ToList();
        var errored = results.Where(r => r.Error is not null).ToList();

        var aggregate = new AggregateMetrics
        {
            K = _options.K,
            EvaluatedCaseCount = ok.Count,
            ErroredCaseCount = errored.Count,
            HitAtK = SafeMean(ok, r => r.Metrics.HitAtK),
            MeanPrecisionAtK = SafeMean(ok, r => r.Metrics.PrecisionAtK),
            MeanRecallAtK = SafeMean(ok, r => r.Metrics.RecallAtK),
            Mrr = SafeMean(ok, r => r.Metrics.ReciprocalRank),
            Map = SafeMean(ok, r => r.Metrics.AveragePrecision),
            MeanNdcgAtK = SafeMean(ok, r => r.Metrics.NdcgAtK),
            MeanF1AtK = SafeMean(ok, r => r.Metrics.F1AtK),
        };

        return new EvalReport
        {
            RunAt = runAt,
            Aggregate = aggregate,
            Results = results,
            Options = _options,
            RetrieverName = _retriever.GetType().Name,
            GraderName = _grader.GetType().Name,
        };
    }

    private static double SafeMean(List<QueryResult> results, Func<QueryResult, double> selector)
    {
        if (results.Count == 0) return double.NaN;
        double sum = 0;
        foreach (var r in results) sum += selector(r);
        return sum / results.Count;
    }
}
