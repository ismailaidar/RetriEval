using System.CommandLine;
using System.Text.Json;
using RetriEval.Core;
using RetriEval.Reporting;

namespace RetriEval.Cli.Commands;

internal static class RunCommand
{
    internal static Command Build()
    {
        var goldenSetOption = new Option<string>(
            "--golden-set", () => "golden-set.json", "Path to the golden-set JSON file.");
        var outputOption = new Option<string>(
            "--output", () => "retrieval-report", "Base path for output reports (without extension).");
        var kOption = new Option<int>(
            "--k", () => 5, "Number of results to retrieve per query.");
        var formatOption = new Option<string[]>(
            "--format", () => ["markdown", "json"],
            "Output format(s): markdown, json, html. Repeat for multiple.")
        { AllowMultipleArgumentsPerToken = true };

        var cmd = new Command("run",
            "Run a retrieval evaluation using the in-memory FakeRetriever (for local smoke tests). " +
            "Wire a real IRetriever by using RetriEval.Core from code.");
        cmd.AddOption(goldenSetOption);
        cmd.AddOption(outputOption);
        cmd.AddOption(kOption);
        cmd.AddOption(formatOption);

        cmd.SetHandler(async (string goldenSetPath, string outputBase, int k, string[] formats) =>
        {
            if (!File.Exists(goldenSetPath))
            {
                Console.Error.WriteLine($"Golden set not found: {goldenSetPath}");
                Environment.Exit(1);
                return;
            }

            List<GoldenCase>? cases;
            try
            {
                var json = await File.ReadAllTextAsync(goldenSetPath);
                cases = JsonSerializer.Deserialize<List<GoldenCase>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to parse golden set: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            if (cases is null or { Count: 0 })
            {
                Console.Error.WriteLine("Golden set is empty.");
                Environment.Exit(1);
                return;
            }

            // Build a corpus from the relevant chunk IDs in the golden set (for smoke testing).
            var corpusIds = cases.SelectMany(c => c.RelevantChunkIds).Distinct().ToList();
            var corpus = corpusIds.Select(id => new RetrievedChunk(id,
                $"Synthetic content for {id}. " + string.Join(" ", id.Split('-')), 0.0)).ToList();

            var retriever = new InMemoryRetriever(corpus);
            var options   = new EvalOptions { K = k, Observer = new ConsoleEvalObserver() };
            var runner    = new EvalRunner(retriever, ChunkIdGrader.Instance, options);

            Console.WriteLine($"Running evaluation: {cases.Count} cases, k={k}");
            var report = await runner.RunAsync(cases);
            Console.WriteLine();
            PrintAggregate(report);
            await WriteReportsAsync(report, outputBase, formats);
        }, goldenSetOption, outputOption, kOption, formatOption);

        return cmd;
    }

    private static void PrintAggregate(EvalReport report)
    {
        var a = report.Aggregate;
        Console.WriteLine($"=== Aggregate (k={a.K}) ===");
        Console.WriteLine($"  Hit@{a.K}     : {a.HitAtK:F4}");
        Console.WriteLine($"  MRR       : {a.Mrr:F4}");
        Console.WriteLine($"  MAP       : {a.Map:F4}");
        Console.WriteLine($"  NDCG@{a.K}  : {a.MeanNdcgAtK:F4}");
        Console.WriteLine($"  Cases     : {a.EvaluatedCaseCount} ok, {a.ErroredCaseCount} errored");
    }

    private static async Task WriteReportsAsync(EvalReport report, string outputBase, string[] formats)
    {
        foreach (var fmt in formats.Select(f => f.ToLowerInvariant()))
        {
            switch (fmt)
            {
                case "markdown" or "md":
                    var mdPath = outputBase + ".md";
                    await new MarkdownReporter().WriteAsync(report, mdPath);
                    Console.WriteLine($"Markdown report: {Path.GetFullPath(mdPath)}");
                    break;
                case "json":
                    var jsonPath = outputBase + ".json";
                    await new JsonReporter().WriteAsync(report, jsonPath);
                    Console.WriteLine($"JSON report    : {Path.GetFullPath(jsonPath)}");
                    break;
                case "html":
                    var htmlPath = outputBase + ".html";
                    await new HtmlReporter().WriteAsync(report, htmlPath);
                    Console.WriteLine($"HTML report    : {Path.GetFullPath(htmlPath)}");
                    break;
                default:
                    Console.Error.WriteLine($"Unknown format: {fmt}. Supported: markdown, json, html.");
                    break;
            }
        }
    }
}
