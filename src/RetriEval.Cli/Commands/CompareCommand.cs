using System.CommandLine;
using RetriEval.Reporting;

namespace RetriEval.Cli.Commands;

internal static class CompareCommand
{
    internal static Command Build()
    {
        var baselineArg = new Argument<string>("baseline", "Path to the baseline JSON report.");
        var currentArg  = new Argument<string>("current",  "Path to the current JSON report.");
        var failOnRegressionOption = new Option<bool>(
            "--fail-on-regression", () => false,
            "Exit with code 1 when regressions are detected (useful as a CI gate).");

        var cmd = new Command("compare",
            "Diff two JSON report files and surface regressions, improvements, and aggregate deltas.");
        cmd.AddArgument(baselineArg);
        cmd.AddArgument(currentArg);
        cmd.AddOption(failOnRegressionOption);

        cmd.SetHandler(async (string baseline, string current, bool failOnRegression) =>
        {
            EvalReportDto baselineDto, currentDto;
            try
            {
                baselineDto = await JsonReporter.ReadAsync(baseline);
                currentDto  = await JsonReporter.ReadAsync(current);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to read report(s): {ex.Message}");
                Environment.Exit(2);
                return;
            }

            // Reconstruct lightweight EvalReports from DTOs for the comparer.
            var baselineReport = DtoToReport(baselineDto);
            var currentReport  = DtoToReport(currentDto);

            var diff = new RegressionComparer().Compare(baselineReport, currentReport);
            PrintDiff(diff);

            if (failOnRegression && diff.HasRegressions)
                Environment.Exit(1);

        }, baselineArg, currentArg, failOnRegressionOption);

        return cmd;
    }

    private static void PrintDiff(RegressionReport diff)
    {
        var agg = diff.AggregateDelta;
        Console.WriteLine("=== Aggregate Δ (current − baseline) ===");
        Console.WriteLine($"  Hit@k   : {Sign(agg.HitAtK)}{agg.HitAtK:F4}");
        Console.WriteLine($"  MRR     : {Sign(agg.Mrr)}{agg.Mrr:F4}");
        Console.WriteLine($"  MAP     : {Sign(agg.Map)}{agg.Map:F4}");
        Console.WriteLine($"  NDCG@k  : {Sign(agg.MeanNdcgAtK)}{agg.MeanNdcgAtK:F4}");
        Console.WriteLine();

        if (diff.RegressedCases.Count > 0)
        {
            Console.WriteLine($"Regressions ({diff.RegressedCases.Count}):");
            foreach (var d in diff.RegressedCases)
                Console.WriteLine($"  ❌ {d.CaseId}  NDCG Δ={d.MetricDelta?.NdcgAtK:+0.0000;-0.0000}");
        }

        if (diff.ImprovedCases.Count > 0)
        {
            Console.WriteLine($"Improvements ({diff.ImprovedCases.Count}):");
            foreach (var d in diff.ImprovedCases)
                Console.WriteLine($"  ✅ {d.CaseId}  NDCG Δ={d.MetricDelta?.NdcgAtK:+0.0000;-0.0000}");
        }

        if (!diff.HasRegressions && diff.ImprovedCases.Count == 0)
            Console.WriteLine("No significant changes detected.");
    }

    private static string Sign(double v) => v >= 0 ? "+" : "";

    private static RetriEval.Core.EvalReport DtoToReport(EvalReportDto dto)
    {
        var results = dto.Results.Select(r =>
        {
            var metrics = r.Metrics ?? new RetriEval.Core.QueryMetrics
            {
                K = dto.K, HitAtK = 0, PrecisionAtK = 0, RecallAtK = 0,
                ReciprocalRank = 0, AveragePrecision = 0, NdcgAtK = 0, F1AtK = 0,
            };
            return new RetriEval.Core.QueryResult
            {
                CaseId = r.CaseId,
                Query  = r.Query,
                Retrieved = r.Retrieved.Select(c => new RetriEval.Core.RetrievedChunk(c.Id, "", c.Score)).ToList(),
                Relevance = r.Relevance,
                Gains     = r.Gains,
                Metrics   = metrics,
                Category  = r.Category,
                Error     = r.Error is not null ? new Exception(r.Error) : null,
            };
        }).ToList();

        var agg = dto.Aggregate ?? new RetriEval.Core.AggregateMetrics
        {
            K = dto.K, HitAtK = 0, MeanPrecisionAtK = 0, MeanRecallAtK = 0,
            Mrr = 0, Map = 0, MeanNdcgAtK = 0, MeanF1AtK = 0,
            EvaluatedCaseCount = results.Count(r => r.Error is null),
            ErroredCaseCount   = results.Count(r => r.Error is not null),
        };

        return new RetriEval.Core.EvalReport
        {
            RunAt         = dto.RunAt,
            Aggregate     = agg,
            Results       = results,
            Options       = new RetriEval.Core.EvalOptions { K = dto.K },
            RetrieverName = dto.RetrieverName,
            GraderName    = dto.GraderName,
        };
    }
}
