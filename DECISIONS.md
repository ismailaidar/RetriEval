# Architecture Decisions

## M1 — Core MVP

### D1 — Use `.slnx` (XML solution format)
The repo targets .NET 8 *runtime* but the .NET 10 SDK is available, which supports the new
XML-based `.slnx` solution format. Using `.slnx` gives a human-readable, merge-friendly
solution file without the GUIDs of `.sln`.

### D2 — `InMemoryRetriever` uses word-overlap scoring (not BM25/TF-IDF)
A full BM25 implementation would add complexity with no benefit for samples/tests. The simple
"count matching query words" scoring is deterministic, obvious, and sufficient for demonstrating
the library. Real users always bring their own retriever.

### D3 — `CountTotalRelevant` in `EvalRunner` uses `RelevantChunkIds.Count` as the denominator for Recall
Recall@k requires a denominator (how many relevant items exist in total). For `KeywordGrader`,
the true relevant set size is unknowable without scanning the entire corpus. We fall back to
`RelevantChunkIds.Count` (at least 1) so Recall stays bounded in [0, 1]. This is documented
in the XML doc comment and README as a known-item caveat.

### D4 — Default interface method `IGrader.Gain` requires casting in concrete classes
C# default interface members are only accessible via interface-typed references. `ChunkIdGrader`
overrides `Gain` explicitly (needed for graded relevance). `KeywordGrader` relies on the default,
which means test code must use `IGrader grader = KeywordGrader.Instance` to call `Gain` on it.
This is documented in the grader tests.

### D5 — `EvalRunner` uses `Parallel.ForEachAsync` with `MaxDegreeOfParallelism`
`Parallel.ForEachAsync` naturally handles backpressure and respects cancellation tokens without
needing a manual `SemaphoreSlim`. Chosen over `Task.WhenAll` for cleaner cancellation propagation.

### D6 — SourceLink warnings when no git repo exists
SourceLink emits a build-time warning (not error) when no git repository is found. These warnings
appear during development before `git init` is run. They are suppressed once the repo exists and
`TreatWarningsAsErrors` in `Directory.Build.props` only applies to C# compiler warnings (not MSBuild
task warnings), so they never block the build.

## M2 — Test ergonomics + richer reporting

### D7 — `RetriEval.Reporting` is a separate package (not folded into Core)
JSON serialization (`System.Text.Json`) and HTML templating add surface area. Keeping them in a
separate package preserves the Core zero-dependency guarantee and lets users who only need the
Markdown reporter avoid the reporting package entirely.

### D8 — `HtmlReporter` uses inline CSS, no external assets
The HTML report is self-contained (single `.html` file) so it can be attached to a PR comment,
emailed, or archived without hosting. No JS framework, no CDN calls.

### D9 — xUnit and NUnit wrappers are thin facade packages
The testing packages only depend on Core + the respective test framework. They do not re-implement
any metric logic — they call `RetrievalMetrics` directly. This means metric bugs are fixed once
in Core and automatically reflected in both assertion wrappers.
