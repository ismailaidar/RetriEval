using RetriEval.Core;

// ---------------------------------------------------------------------------
// RetriEval — end-to-end sample
// Runs a retrieval evaluation against an in-memory corpus with zero external dependencies.
// ---------------------------------------------------------------------------

Console.WriteLine("=== RetriEval Sample ===");
Console.WriteLine();

// 1. Define a small corpus of chunks (simulates your document store).
RetrievedChunk[] corpus =
[
    new("chunk-eiffel-1",  "The Eiffel Tower is located in Paris, France. It was built in 1889.", 0),
    new("chunk-eiffel-2",  "Gustave Eiffel designed the tower as the entrance arch for the 1889 World's Fair.", 0),
    new("chunk-louvre-1",  "The Louvre Museum in Paris is the world's largest art museum.", 0),
    new("chunk-louvre-2",  "The Louvre houses the Mona Lisa, painted by Leonardo da Vinci.", 0),
    new("chunk-berlin-1",  "Berlin is the capital and largest city of Germany.", 0),
    new("chunk-berlin-2",  "The Berlin Wall divided the city from 1961 until 1989.", 0),
    new("chunk-python-1",  "Python is a high-level, general-purpose programming language.", 0),
    new("chunk-dotnet-1",  "The .NET runtime supports C#, F#, and Visual Basic.", 0),
];

// 2. Use the built-in InMemoryRetriever (keyword scoring — no ML required).
var retriever = new InMemoryRetriever(corpus);

// 3. Define a golden set — hand-labeled evaluation cases.
GoldenCase[] goldenSet =
[
    new()
    {
        Id = "q-eiffel-location",
        Query = "Where is the Eiffel Tower located?",
        RelevantChunkIds = ["chunk-eiffel-1"],
        Description = "Basic location fact",
        Category = "geography",
    },
    new()
    {
        Id = "q-eiffel-designer",
        Query = "Who designed the Eiffel Tower?",
        RelevantChunkIds = ["chunk-eiffel-2"],
        Description = "Attribution question",
        Category = "geography",
    },
    new()
    {
        Id = "q-louvre",
        Query = "What is in the Louvre museum Paris?",
        RelevantChunkIds = ["chunk-louvre-1", "chunk-louvre-2"],
        Description = "Multiple relevant chunks",
        Category = "culture",
    },
    new()
    {
        Id = "q-berlin-wall",
        Query = "When was the Berlin Wall built?",
        RelevantChunkIds = ["chunk-berlin-2"],
        Description = "Historical fact",
        Category = "history",
    },
    new()
    {
        Id = "q-dotnet",
        Query = "What languages does .NET support?",
        RelevantChunkIds = ["chunk-dotnet-1"],
        RelevantKeywords = ["C#", "F#"],
        Description = "Tech question with keyword fallback",
        Category = "technology",
    },
];

// 4. Configure the runner.
var options = new EvalOptions
{
    K = 3,
    MaxConcurrency = 4,
    Observer = new ConsoleEvalObserver(),
};

var runner = new EvalRunner(
    retriever,
    grader: ChunkIdGrader.Instance,
    options);

// 5. Run the evaluation.
Console.WriteLine("Running evaluation...");
Console.WriteLine();
var report = await runner.RunAsync(goldenSet);
Console.WriteLine();

// 6. Print aggregate metrics to stdout.
Console.WriteLine("=== Aggregate Metrics (k={0}) ===", report.Options.K);
Console.WriteLine($"  Hit@{report.Options.K}    : {report.Aggregate.HitAtK:F4}");
Console.WriteLine($"  MRR       : {report.Aggregate.Mrr:F4}");
Console.WriteLine($"  MAP       : {report.Aggregate.Map:F4}");
Console.WriteLine($"  NDCG@{report.Options.K}  : {report.Aggregate.MeanNdcgAtK:F4}");
Console.WriteLine($"  P@{report.Options.K}      : {report.Aggregate.MeanPrecisionAtK:F4}");
Console.WriteLine($"  R@{report.Options.K}      : {report.Aggregate.MeanRecallAtK:F4}");
Console.WriteLine($"  F1@{report.Options.K}     : {report.Aggregate.MeanF1AtK:F4}");
Console.WriteLine($"  Evaluated : {report.Aggregate.EvaluatedCaseCount} cases");
Console.WriteLine($"  Errors    : {report.Aggregate.ErroredCaseCount} cases");
Console.WriteLine();

// 7. Write a Markdown report to disk.
var reporter = new MarkdownReporter(new MarkdownReporterOptions
{
    Thresholds = new MetricThresholds
    {
        HitAtK  = 0.60,
        Mrr     = 0.50,
        Map     = 0.50,
        NdcgAtK = 0.50,
    },
});

var reportPath = "retrieval-report.md";
await reporter.WriteAsync(report, reportPath);
Console.WriteLine($"Markdown report written to: {Path.GetFullPath(reportPath)}");
