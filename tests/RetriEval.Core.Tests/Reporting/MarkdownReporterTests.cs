using RetriEval.Core;

namespace RetriEval.Core.Tests.Reporting;

public class MarkdownReporterTests
{
    private static EvalReport BuildReport(bool withError = false)
    {
        var metrics = new QueryMetrics
        {
            K = 3, HitAtK = 1.0, PrecisionAtK = 0.333, RecallAtK = 1.0,
            ReciprocalRank = 1.0, AveragePrecision = 1.0, NdcgAtK = 1.0, F1AtK = 0.5,
        };
        var results = new List<QueryResult>
        {
            new()
            {
                CaseId = "case-1", Query = "test query",
                Retrieved = [new("c1", "content", 0.9)],
                Relevance = [true], Gains = [1],
                Metrics = metrics,
            },
        };
        if (withError)
        {
            results.Add(new QueryResult
            {
                CaseId = "case-err", Query = "bad query",
                Retrieved = [], Relevance = [], Gains = [],
                Metrics = new QueryMetrics { K = 3, HitAtK = 0, PrecisionAtK = 0, RecallAtK = 0, ReciprocalRank = 0, AveragePrecision = 0, NdcgAtK = 0, F1AtK = 0 },
                Error = new InvalidOperationException("Retriever failed"),
            });
        }

        var agg = new AggregateMetrics
        {
            K = 3, HitAtK = 1.0, MeanPrecisionAtK = 0.333, MeanRecallAtK = 1.0,
            Mrr = 1.0, Map = 1.0, MeanNdcgAtK = 1.0, MeanF1AtK = 0.5,
            EvaluatedCaseCount = 1, ErroredCaseCount = withError ? 1 : 0,
        };

        return new EvalReport
        {
            RunAt = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero),
            Aggregate = agg,
            Results = results,
            Options = new EvalOptions { K = 3 },
            RetrieverName = "TestRetriever",
            GraderName = "ChunkIdGrader",
        };
    }

    [Fact]
    public void Render_ContainsHeader()
    {
        var reporter = new MarkdownReporter();
        var md = reporter.Render(BuildReport());
        Assert.Contains("# RetriEval Report", md);
    }

    [Fact]
    public void Render_ContainsRetrieverName()
    {
        var reporter = new MarkdownReporter();
        var md = reporter.Render(BuildReport());
        Assert.Contains("TestRetriever", md);
    }

    [Fact]
    public void Render_ContainsAggregateTable()
    {
        var reporter = new MarkdownReporter();
        var md = reporter.Render(BuildReport());
        Assert.Contains("| MRR |", md);
        Assert.Contains("| MAP |", md);
    }

    [Fact]
    public void Render_ContainsCaseId()
    {
        var reporter = new MarkdownReporter();
        var md = reporter.Render(BuildReport());
        Assert.Contains("case-1", md);
    }

    [Fact]
    public void Render_WithError_ContainsErrorSection()
    {
        var reporter = new MarkdownReporter();
        var md = reporter.Render(BuildReport(withError: true));
        Assert.Contains("Errored Cases", md);
        Assert.Contains("case-err", md);
    }

    [Fact]
    public void Render_WithThreshold_ShowsPassFail()
    {
        var reporter = new MarkdownReporter(new MarkdownReporterOptions
        {
            Thresholds = new MetricThresholds { HitAtK = 0.8 },
        });
        var md = reporter.Render(BuildReport());
        Assert.Contains("pass", md);
    }

    [Fact]
    public void Render_ThresholdNotMet_ShowsFail()
    {
        var reporter = new MarkdownReporter(new MarkdownReporterOptions
        {
            Thresholds = new MetricThresholds { HitAtK = 0.99 },
        });
        // HitAtK in report is 1.0 (passes), but MRR threshold isn't set. Let's set MRR high.
        var reporter2 = new MarkdownReporter(new MarkdownReporterOptions
        {
            Thresholds = new MetricThresholds { Mrr = 2.0 }, // impossible threshold
        });
        var md = reporter2.Render(BuildReport());
        Assert.Contains("fail", md);
    }

    [Fact]
    public void Render_NullReport_ThrowsArgumentNull()
    {
        var reporter = new MarkdownReporter();
        Assert.Throws<ArgumentNullException>(() => reporter.Render(null!));
    }
}
