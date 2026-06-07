# RetriEval

**Deterministic, CI-friendly retrieval evaluation for .NET RAG systems.**

Score Hit@k, MRR, MAP, NDCG, Precision, Recall, and F1 against a hand-labeled golden set. Reproducible, free to run, and wirable into a CI gate — no cloud credentials required.

[![NuGet](https://img.shields.io/nuget/v/RetriEval.Core)](https://www.nuget.org/packages/RetriEval.Core)
[![CI](https://github.com/ismailaidar/RetriEval/actions/workflows/ci.yml/badge.svg)](https://github.com/ismailaidar/RetriEval/actions/workflows/ci.yml)

---

## 60-second quickstart

```csharp
// 1. Add the package:
//    dotnet add package RetriEval.Core

// 2. Implement IRetriever (or use an adapter package):
public class MyRetriever : IRetriever
{
    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query, int k, CancellationToken ct = default)
    {
        // Call your vector store, search index, etc.
        // Return chunks ordered by descending relevance score.
        var results = await _searchClient.SearchAsync(query, k, ct);
        return results.Select(r => new RetrievedChunk(r.Id, r.Content, r.Score)).ToList();
    }
}

// 3. Write 5 golden cases (hand-label these):
GoldenCase[] goldenSet =
[
    new() { Id = "q1", Query = "What is the Eiffel Tower?",
            RelevantChunkIds = ["chunk-eiffel-facts"] },
    new() { Id = "q2", Query = "Who built the Louvre?",
            RelevantChunkIds = ["chunk-louvre-history"] },
    // ...
];

// 4. Run the evaluation:
var runner = new EvalRunner(
    new MyRetriever(),
    grader: ChunkIdGrader.Instance,
    options: new EvalOptions { K = 5 });

var report = await runner.RunAsync(goldenSet);

// 5. Write a Markdown report and assert a CI gate:
var reporter = new MarkdownReporter(new MarkdownReporterOptions
{
    Thresholds = new MetricThresholds { HitAtK = 0.80, Mrr = 0.60 }
});
await reporter.WriteAsync(report, "retrieval-report.md");

if (report.Aggregate.HitAtK < 0.80)
    throw new Exception($"Hit@5 below threshold: {report.Aggregate.HitAtK:F3}");
```

---

## Why deterministic over LLM-judge?

The Python RAG ecosystem (Ragas, DeepEval, TruLens) defaults to LLM-as-judge for retrieval scoring. That approach:

- Costs money on every CI run
- Is non-deterministic (same inputs can yield different grades across runs)
- Requires credentials that make local dev painful
- Is slow (seconds per query vs. microseconds)

RetriEval takes the opposite position: **ground truth is a spreadsheet, not a prompt.** Hand-label 50–200 queries once, commit them to source control, and run the eval in under a second in every CI job — free, reproducible, and auditable. LLM-based grading (`LlmJudgeGrader`, see below) is available as an add-on when you need it, but is never required.

---

## Metrics

All metrics operate on a per-query relevance signal and return a per-query value plus a mean aggregate. The math functions are pure static methods on `RetrievalMetrics` and are independently unit-testable.

| Metric | Formula | Notes |
|--------|---------|-------|
| **Hit@k** | 1 if any relevant in top-k, else 0 | Aggregate = % queries with at least one hit |
| **Precision@k** | relevant-in-top-k / k | |
| **Recall@k** | relevant-in-top-k / \|relevant-set\| | See caveat below |
| **MRR** | mean of 1/rank-of-first-relevant | 0 if no relevant found |
| **MAP** | mean of Average Precision | AP = mean P@i at relevant positions |
| **NDCG@k** | DCG@k / IDCG@k | Supports graded gains via `GradedRelevance` |
| **F1@k** | harmonic mean of P@k and R@k | |

### Known-item Recall caveat

Recall@k is measured against the **hand-labeled** relevant set, not the entire corpus. The corpus may contain unlabeled relevant chunks that this metric cannot detect. Treat Recall@k as a lower bound — if Recall@k = 1.0, it means every labeled relevant item was retrieved, not that retrieval is perfect.

---

## Grader strategies

A `IGrader` decides whether a retrieved chunk satisfies a golden case. Choose the strategy that matches your corpus stability:

| Grader | How it works | When to use |
|--------|-------------|-------------|
| `ChunkIdGrader` | Exact match on `RetrievedChunk.Id` | Stable chunk ids (recommended) |
| `KeywordGrader` | Chunk content contains any `RelevantKeyword` (case-insensitive) | Ids change after re-chunking |
| `SemanticGrader` | Cosine similarity ≥ threshold via `IEmbedder` | Paraphrased chunks |
| `LlmJudgeGrader` | LLM decides per-chunk relevance | Maximum precision, non-deterministic |

> **`KeywordGrader` caveat — keyword presence ≠ query relevance.**
> A query "Does Humira require prior authorization?" with keyword `"Humira"` would mark a
> chunk about storage temperature as relevant, because it contains the word. Use
> `ChunkIdGrader` (stable ids) or `LlmJudgeGrader` (semantic judgement) when false positives matter.

You can combine strategies: run `ChunkIdGrader` in CI (fast, deterministic), and spot-check with `LlmJudgeGrader` locally.

### LLM-judge grading (0.2.0+)

`RetriEval.Llm.Abstractions` ships `OpenAILlmClient` — a zero-extra-dependency implementation of `ILlmClient` that works with OpenAI, Azure OpenAI, Ollama, and any OpenAI-compatible endpoint:

```csharp
// dotnet add package RetriEval.Llm.Abstractions

// OpenAI
var llm = new OpenAILlmClient(apiKey: Environment.GetEnvironmentVariable("OPENAI_KEY")!);

// Azure OpenAI (resource-key auth)
var llm = new OpenAILlmClient(
    apiKey: Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")!,
    endpoint: "https://{resource}.openai.azure.com/openai/deployments/{deployment}" +
              "/chat/completions?api-version=2024-02-01",
    authHeaderName: "api-key");

// Ollama (local, no key needed)
var llm = new OpenAILlmClient(apiKey: "ollama", model: "llama3",
    endpoint: "http://localhost:11434/v1/chat/completions");

var grader = new LlmJudgeGrader(llm);
var runner = new EvalRunner(retriever, grader);
```

`LlmJudgeGrader` implements `IAsyncGrader` — `EvalRunner` automatically uses the async path, so LLM calls are properly awaited with no sync-over-async overhead.

---

## Solution structure

```
src/
  RetriEval.Core/                     # Metrics, models, runner, graders, GoldenSetLoader — zero runtime deps
  RetriEval.Reporting/                # JSON + HTML reporters, regression comparer
  RetriEval.Testing.Xunit/            # xUnit assertion wrappers (RetrievalAssert)
  RetriEval.Testing.NUnit/            # NUnit assertion wrappers (RetrievalAssert)
  RetriEval.Embeddings.Abstractions/  # IEmbedder + SemanticGrader
  RetriEval.Llm.Abstractions/         # ILlmClient + OpenAILlmClient + LlmJudgeGrader + GoldenSetGenerator
  RetriEval.Adapters.AzureAISearch/   # Azure AI Search adapter (AzureAISearchRetriever)
  RetriEval.Adapters.Qdrant/          # Qdrant adapter (QdrantRetriever)
  RetriEval.Observability/            # OpenTelemetry IEvalObserver exporter
  RetriEval.Cli/                      # `retrieval-eval` dotnet tool: init, run, compare, generate
tests/
  RetriEval.Core.Tests/
  RetriEval.Reporting.Tests/
  RetriEval.Adapters.Tests/   # Behind [Trait("Category","Live")], skipped without creds
samples/
  RetriEval.Sample/       # Zero-dependency runnable demo
```

---

## Running the sample

```bash
dotnet run --project samples/RetriEval.Sample
```

Produces console output with aggregate metrics and writes `retrieval-report.md`. No external services, no API keys.

---

## Core API

### Models

```csharp
record RetrievedChunk(string Id, string Content, double Score,
    IReadOnlyDictionary<string, string>? Metadata = null);

record GoldenCase
{
    required string Id;
    required string Query;
    IReadOnlyList<string> RelevantChunkIds = [];
    IReadOnlyList<string> RelevantKeywords = [];
    IReadOnlyDictionary<string, int>? GradedRelevance;   // chunkId → gain, for NDCG
    string? Category;
    string? Description;
}
```

Load and save golden sets without hand-rolling JSON (de)serialization — `GoldenSetLoader`
matches the format produced by `retrieval-eval init` (camelCase, case-insensitive on read):

```csharp
IReadOnlyList<GoldenCase> goldenSet = await GoldenSetLoader.LoadAsync("golden-set.json");
await GoldenSetLoader.SaveAsync(goldenSet, "golden-set.json");
```

> **Pitfall — casing is normalized, separators are not.** `relevantChunkIds` and
> `RelevantChunkIds` both bind correctly, but `relevant_chunk_ids` (snake_case) does **not** —
> the underscore makes it a different property name, so it silently deserializes to an empty
> collection instead of throwing. `LoadAsync` specifically detects cases left with *no*
> relevance signal at all (empty `RelevantChunkIds`, `RelevantKeywords`, *and*
> `GradedRelevance`) and throws `InvalidDataException` naming the offending case ids — so a
> naming mismatch surfaces immediately instead of producing a run that mysteriously scores
> zero across the board. Use camelCase keys to match `retrieval-eval init`'s output.

### Interfaces

```csharp
interface IRetriever
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int k, CancellationToken ct);
}

interface IGrader
{
    bool IsRelevant(RetrievedChunk chunk, GoldenCase @case);
    int Gain(RetrievedChunk chunk, GoldenCase @case);   // for NDCG; defaults to binary
}

// Implement instead of IGrader when relevance judgement involves I/O (e.g. LLM calls).
// EvalRunner detects IAsyncGrader and uses the async path automatically.
interface IAsyncGrader : IGrader
{
    Task<bool> IsRelevantAsync(RetrievedChunk chunk, GoldenCase @case, CancellationToken ct);
    Task<int>  GainAsync(RetrievedChunk chunk, GoldenCase @case, CancellationToken ct);
}

interface IEvalObserver
{
    void OnCaseStarted(GoldenCase goldenCase);
    void OnCaseCompleted(QueryResult result);
    void OnRunCompleted(EvalReport report);
}
```

### EvalRunner

```csharp
var runner = new EvalRunner(retriever, grader, new EvalOptions
{
    K              = 5,              // default: 3
    MaxConcurrency = 8,              // default: 4
    MaxRetries     = 2,              // default: 2 (exponential backoff)
    Observer       = new ConsoleEvalObserver(),
});
EvalReport report = await runner.RunAsync(goldenSet, cancellationToken);
```

### EvalReport

```csharp
report.Aggregate.HitAtK         // mean Hit@k across all cases
report.Aggregate.Mrr            // MRR
report.Aggregate.Map            // MAP
report.Aggregate.MeanNdcgAtK    // mean NDCG@k
report.Results                  // per-query breakdown
report.ErroredResults           // cases where retriever threw
```

---

## CI gate example (xUnit)

```csharp
[Fact]
public async Task Retrieval_MeetsQualityBar()
{
    var report = await _runner.RunAsync(_goldenSet);

    var reporter = new MarkdownReporter();
    await reporter.WriteAsync(report, "retrieval-report.md");  // attach as CI artifact

    Assert.True(report.Aggregate.HitAtK  >= 0.80, $"Hit@k={report.Aggregate.HitAtK:F3}");
    Assert.True(report.Aggregate.Mrr     >= 0.60, $"MRR={report.Aggregate.Mrr:F3}");
    Assert.Equal(0, report.Aggregate.ErroredCaseCount);
}
```

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for what changed in each release — useful for deciding
when to adopt a new minor version versus pinning.

## License

MIT — see [LICENSE](LICENSE).
