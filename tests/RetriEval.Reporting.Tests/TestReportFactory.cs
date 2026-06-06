using RetriEval.Core;

namespace RetriEval.Reporting.Tests;

internal static class TestReportFactory
{
    internal static EvalReport BuildError(string caseId = "case-1")
    {
        var zeroMetrics = new QueryMetrics
        {
            K = 3, HitAtK = 0, PrecisionAtK = 0, RecallAtK = 0,
            ReciprocalRank = 0, AveragePrecision = 0, NdcgAtK = 0, F1AtK = 0,
        };
        var results = new List<QueryResult>
        {
            new()
            {
                CaseId = caseId, Query = "test query",
                Retrieved = [], Relevance = [], Gains = [],
                Metrics = zeroMetrics,
                Error = new InvalidOperationException("Simulated error"),
            },
        };
        var agg = new AggregateMetrics
        {
            K = 3, HitAtK = 0, MeanPrecisionAtK = 0, MeanRecallAtK = 0,
            Mrr = 0, Map = 0, MeanNdcgAtK = 0, MeanF1AtK = 0,
            EvaluatedCaseCount = 0, ErroredCaseCount = 1,
        };
        return new EvalReport
        {
            RunAt = new DateTimeOffset(2024, 6, 1, 13, 0, 0, TimeSpan.Zero),
            Aggregate = agg, Results = results,
            Options = new EvalOptions { K = 3 },
            RetrieverName = "TestRetriever", GraderName = "ChunkIdGrader",
        };
    }

    internal static EvalReport Build(
        string caseId = "case-1",
        string query = "test query",
        double hitAtK = 1.0,
        double mrr = 1.0,
        double ndcg = 1.0,
        bool withError = false)
    {
        var metrics = new QueryMetrics
        {
            K = 3,
            HitAtK = hitAtK,
            PrecisionAtK = 0.333,
            RecallAtK = 1.0,
            ReciprocalRank = mrr,
            AveragePrecision = 1.0,
            NdcgAtK = ndcg,
            F1AtK = 0.5,
        };

        var results = new List<QueryResult>
        {
            new()
            {
                CaseId = caseId,
                Query = query,
                Retrieved = [new("c1", "content", 0.9)],
                Relevance = [true],
                Gains = [1],
                Metrics = metrics,
            },
        };

        if (withError)
        {
            results.Add(new QueryResult
            {
                CaseId = "case-err",
                Query = "bad query",
                Retrieved = [],
                Relevance = [],
                Gains = [],
                Metrics = new QueryMetrics
                {
                    K = 3, HitAtK = 0, PrecisionAtK = 0, RecallAtK = 0,
                    ReciprocalRank = 0, AveragePrecision = 0, NdcgAtK = 0, F1AtK = 0,
                },
                Error = new InvalidOperationException("Simulated error"),
            });
        }

        var agg = new AggregateMetrics
        {
            K = 3,
            HitAtK = hitAtK,
            MeanPrecisionAtK = 0.333,
            MeanRecallAtK = 1.0,
            Mrr = mrr,
            Map = 1.0,
            MeanNdcgAtK = ndcg,
            MeanF1AtK = 0.5,
            EvaluatedCaseCount = 1,
            ErroredCaseCount = withError ? 1 : 0,
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
}
