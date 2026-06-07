using RetriEval.Core;

namespace RetriEval.Core.Tests.Runner;

public class EvalRunnerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static GoldenCase MakeCase(string id, params string[] relevantIds) => new()
    {
        Id = id,
        Query = $"query for {id}",
        RelevantChunkIds = relevantIds,
    };

    private static IRetriever StaticRetriever(IReadOnlyList<RetrievedChunk> chunks) =>
        new FixedRetriever(chunks);

    // ---------------------------------------------------------------------------
    // Basic correctness
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Run_SingleCase_CorrectHitAtK()
    {
        var retriever = StaticRetriever([
            new("chunk-1", "content", 0.9),
            new("chunk-2", "content", 0.5)]);
        var runner = new EvalRunner(retriever, ChunkIdGrader.Instance, new EvalOptions { K = 3 });

        var report = await runner.RunAsync([MakeCase("q1", "chunk-1")]);

        var result = Assert.Single(report.Results);
        Assert.Equal(1.0, result.Metrics.HitAtK);
        Assert.Equal(1.0, report.Aggregate.HitAtK);
    }

    [Fact]
    public async Task Run_ChunkNotRetrieved_ZeroHit()
    {
        var retriever = StaticRetriever([new RetrievedChunk("chunk-99", "content", 0.9)]);
        var runner = new EvalRunner(retriever, ChunkIdGrader.Instance, new EvalOptions { K = 1 });

        var report = await runner.RunAsync([MakeCase("q1", "chunk-wanted")]);

        Assert.Equal(0.0, report.Results[0].Metrics.HitAtK);
    }

    [Fact]
    public async Task Run_MultipleCases_AggregatesCorrectly()
    {
        // Case 1: hit. Case 2: miss.
        var retriever = new TwoQueryRetriever(
            ("query for q1", [new RetrievedChunk("a", "c", 1)]),
            ("query for q2", [new RetrievedChunk("x", "c", 1)]));

        var runner = new EvalRunner(retriever, ChunkIdGrader.Instance, new EvalOptions { K = 1 });
        var report = await runner.RunAsync([MakeCase("q1", "a"), MakeCase("q2", "b")]);

        Assert.Equal(2, report.Results.Count);
        Assert.Equal(0.5, report.Aggregate.HitAtK, 10);
    }

    // ---------------------------------------------------------------------------
    // Error capture — a bad retriever must not abort the entire run
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Run_RetrieverThrows_ErrorCapturedOtherCasesEvaluated()
    {
        var retriever = new FlakyRetriever(failOnQuery: "query for fail");
        var runner = new EvalRunner(retriever, ChunkIdGrader.Instance,
            new EvalOptions { K = 1, MaxRetries = 0 });

        var cases = new[]
        {
            new GoldenCase { Id = "ok",   Query = "query for ok",   RelevantChunkIds = ["c1"] },
            new GoldenCase { Id = "fail", Query = "query for fail", RelevantChunkIds = ["c2"] },
        };

        var report = await runner.RunAsync(cases);

        Assert.Equal(2, report.Results.Count);
        var okResult   = report.Results.First(r => r.CaseId == "ok");
        var failResult = report.Results.First(r => r.CaseId == "fail");

        Assert.Null(okResult.Error);
        Assert.NotNull(failResult.Error);
        Assert.Equal(1, report.Aggregate.ErroredCaseCount);
        Assert.Equal(1, report.Aggregate.EvaluatedCaseCount);
    }

    // ---------------------------------------------------------------------------
    // Observer fires expected events
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Run_Observer_ReceivesStartedCompletedAndRunCompleted()
    {
        var observer = new RecordingObserver();
        var retriever = StaticRetriever([new RetrievedChunk("c1", "content", 1.0)]);
        var runner = new EvalRunner(retriever, ChunkIdGrader.Instance,
            new EvalOptions { K = 1, Observer = observer });

        await runner.RunAsync([MakeCase("q1", "c1")]);

        Assert.Contains("q1", observer.Started);
        Assert.Contains("q1", observer.Completed);
        Assert.True(observer.RunCompleted);
    }

    // ---------------------------------------------------------------------------
    // Relevance flags match retrieved order
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Run_RelevanceFlags_MatchRankOrder()
    {
        var retriever = StaticRetriever([
            new RetrievedChunk("relevant",   "content", 0.9),
            new RetrievedChunk("irrelevant", "content", 0.5)]);
        var runner = new EvalRunner(retriever, ChunkIdGrader.Instance, new EvalOptions { K = 2 });

        var report = await runner.RunAsync([MakeCase("q1", "relevant")]);
        var result = report.Results[0];

        Assert.Equal(2, result.Relevance.Count);
        Assert.True(result.Relevance[0]);
        Assert.False(result.Relevance[1]);
    }

    // ---------------------------------------------------------------------------
    // Recall@k / F1@k must stay within [0, 1] regardless of grading strategy
    // (regression coverage for a bug where keyword-graded cases — which have no
    // RelevantChunkIds to size the relevant set — produced Recall@k > 1.0 and,
    // transitively, F1@k > 1.0; see CHANGELOG 0.3.2).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Run_KeywordGradedCase_RecallAndF1StayWithinUnitRange()
    {
        var retriever = StaticRetriever([
            new RetrievedChunk("c1", "Store Humira at 36-46 F", 0.9),
            new RetrievedChunk("c2", "Humira requires prior authorization", 0.8),
            new RetrievedChunk("c3", "Humira is an injectable biologic", 0.7)]);

        var goldenCase = new GoldenCase
        {
            Id = "q-humira",
            Query = "Does Humira require prior authorization?",
            RelevantKeywords = ["Humira"],
        };

        var runner = new EvalRunner(retriever, KeywordGrader.Instance, new EvalOptions { K = 3 });
        var report = await runner.RunAsync([goldenCase]);

        var metrics = report.Results[0].Metrics;
        Assert.InRange(metrics.RecallAtK, 0.0, 1.0);
        Assert.InRange(metrics.F1AtK, 0.0, 1.0);

        // All three retrieved chunks match the keyword, so the grader's own relevant set
        // is {c1, c2, c3} — recall over that set is 1.0, not 3.0.
        Assert.Equal(1.0, metrics.RecallAtK, 10);
        Assert.Equal(1.0, metrics.F1AtK, 10);
    }

    [Fact]
    public async Task Run_MixedGradingSignals_RecallAndF1NeverExceedOne()
    {
        var retriever = StaticRetriever([
            new RetrievedChunk("a", "Humira dosage info", 1.0),
            new RetrievedChunk("b", "Humira storage info", 0.9),
            new RetrievedChunk("c", "Humira injection guide", 0.8)]);

        var cases = new[]
        {
            new GoldenCase { Id = "ids-only",      Query = "query for ids-only",      RelevantChunkIds = ["a"] },
            new GoldenCase { Id = "keywords-only", Query = "query for keywords-only", RelevantKeywords = ["Humira"] },
            new GoldenCase { Id = "both",          Query = "query for both",          RelevantChunkIds = ["a"], RelevantKeywords = ["Humira"] },
        };

        var runner = new EvalRunner(retriever, KeywordGrader.Instance, new EvalOptions { K = 3 });
        var report = await runner.RunAsync(cases);

        foreach (var result in report.Results)
        {
            Assert.InRange(result.Metrics.RecallAtK, 0.0, 1.0);
            Assert.InRange(result.Metrics.F1AtK, 0.0, 1.0);
        }
        Assert.InRange(report.Aggregate.MeanRecallAtK, 0.0, 1.0);
    }

    // ---------------------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Run_CancelledToken_ThrowsOperationCancelled()
    {
        var retriever = new NeverRetriever();
        var runner = new EvalRunner(retriever, ChunkIdGrader.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync([MakeCase("q1", "c1")], cts.Token));
    }
}

// ---------------------------------------------------------------------------
// Test doubles
// ---------------------------------------------------------------------------

file sealed class FixedRetriever(IReadOnlyList<RetrievedChunk> chunks) : IRetriever
{
    public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int k, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RetrievedChunk>>(chunks.Take(k).ToList());
}

file sealed class TwoQueryRetriever : IRetriever
{
    private readonly Dictionary<string, IReadOnlyList<RetrievedChunk>> _map;

    public TwoQueryRetriever(
        (string query, IReadOnlyList<RetrievedChunk> chunks) first,
        (string query, IReadOnlyList<RetrievedChunk> chunks) second)
    {
        _map = new()
        {
            [first.query] = first.chunks,
            [second.query] = second.chunks,
        };
    }

    public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int k, CancellationToken ct = default)
        => Task.FromResult(_map.TryGetValue(query, out var r) ? r : (IReadOnlyList<RetrievedChunk>)[]);
}

file sealed class FlakyRetriever(string failOnQuery) : IRetriever
{
    public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int k, CancellationToken ct = default)
    {
        if (query == failOnQuery) throw new InvalidOperationException("Simulated retriever failure.");
        IReadOnlyList<RetrievedChunk> result = [new("c1", "content", 1.0)];
        return Task.FromResult(result);
    }
}

file sealed class NeverRetriever : IRetriever
{
    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int k, CancellationToken ct = default)
    {
        await Task.Delay(Timeout.Infinite, ct);
        return [];
    }
}

file sealed class RecordingObserver : IEvalObserver
{
    public List<string> Started { get; } = [];
    public List<string> Completed { get; } = [];
    public bool RunCompleted { get; private set; }

    public void OnCaseStarted(GoldenCase goldenCase) => Started.Add(goldenCase.Id);
    public void OnCaseCompleted(QueryResult result) => Completed.Add(result.CaseId);
    public void OnRunCompleted(EvalReport report) => RunCompleted = true;
}
