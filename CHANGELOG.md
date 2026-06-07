# Changelog

All notable changes to RetriEval are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [SemVer](https://semver.org/).

## [0.3.2] — 2026-06-06

### Fixed
- **`EvalRunner`** no longer produces `Recall@k` (and transitively `F1@k`) values above
  `1.0` for golden cases graded by keyword rather than by chunk id. The relevant-set size
  fed to `RecallAtK` was sized from `RelevantChunkIds.Count` alone — `0` for keyword-only
  cases — while `KeywordGrader` could mark several retrieved chunks relevant, producing
  recall values like `3/1 = 3.0` and `F1@k = 1.5`. The relevant-set size is now the larger
  of the labeled-id count and the number of chunks the grader actually marked relevant,
  which keeps `Recall@k` and `F1@k` within `[0, 1]` for every grading strategy.
- **`RetrievalMetrics.RecallAtK`** now throws `ArgumentOutOfRangeException` if `totalRelevant`
  is smaller than the number of relevant items found in the top-k — an inconsistent input
  that would otherwise silently produce a recall above `1.0`. This also guards `F1AtK`,
  which derives its recall term from `RecallAtK`.
- Reported with a reproduction showing per-query `R@3` values of `1, 0, 2, 3, 1, 0, 2, 1, 2, 3`
  (mean `1.5`) and `F1@3 = 1.5` — both mathematically impossible for ratios bounded by `[0, 1]`.

## [0.3.1] — 2026-06-06

### Fixed
- **`GoldenSetLoader.LoadAsync`** now throws `InvalidDataException` (naming the offending
  case ids) when a golden case deserializes with *no* relevance signal at all — empty
  `RelevantChunkIds`, `RelevantKeywords`, and `GradedRelevance`. Previously this failed
  silently: `System.Text.Json`'s case-insensitive matching normalizes letter casing
  (`relevantChunkIds` ↔ `RelevantChunkIds`) but not separators, so snake_case keys
  (`relevant_chunk_ids`) bound to nothing and left the property at its empty-collection
  default — producing a run that mysteriously scored zero across the board with no error.
  Reported from real debugging time lost chasing a snake_case → camelCase mismatch.

## [0.3.0] — 2026-06-06

### Added
- **`GoldenSetLoader`** (`RetriEval.Core`) — `LoadAsync`/`SaveAsync` for the golden-set JSON
  format used by the `retrieval-eval` CLI, samples, and docs. Matches the camelCase,
  case-insensitive format produced by `retrieval-eval init`, removing the need for consumers
  to hand-roll `JsonSerializer` calls and option setup.

### Changed
- `RetriEval.Cli`'s `run` command now loads golden sets via `GoldenSetLoader` instead of
  inline `JsonSerializer` calls.

### Fixed
- `PackageProjectUrl`/`RepositoryUrl` in `Directory.Build.props` (and the README CI badge)
  pointed at a stale `retri-eval/RetriEval` org instead of the actual repo,
  `ismailaidar/RetriEval`. These URLs are embedded in every published package.

## [0.2.0] — 2026-06-06

### Added
- **`IAsyncGrader`** (`RetriEval.Core`) — interface for graders whose relevance judgement
  involves I/O (e.g. an LLM call). `EvalRunner` detects `IAsyncGrader` implementations and
  uses the async path automatically, avoiding sync-over-async.
- **`OpenAILlmClient`** (`RetriEval.Llm.Abstractions`) — concrete, zero-extra-dependency
  `ILlmClient` for OpenAI, Azure OpenAI (resource-key auth), Ollama, and any
  OpenAI-compatible `/chat/completions` endpoint.
- **`GoldenSetLoader`** (`RetriEval.Core`) — `LoadAsync`/`SaveAsync` for the golden-set JSON
  format used by the `retrieval-eval` CLI, samples, and docs. Removes the need for consumers
  to hand-roll `JsonSerializer` calls and option setup.

### Changed
- **`LlmJudgeGrader`** now implements `IAsyncGrader` (was `IGrader`). `EvalRunner` calls its
  async path directly; the synchronous `IsRelevant` remains for callers that only hold an
  `IGrader` reference, but is no longer the primary path.
- `RetriEval.Cli`'s `run` command now loads golden sets via `GoldenSetLoader` instead of
  inline `JsonSerializer` calls.

### Documentation
- `KeywordGrader` XML docs and README now carry an explicit warning that keyword presence is
  not the same as query relevance (e.g. a query about prior authorization for "Humira" can
  match a chunk that only mentions storage temperature). Recommends `ChunkIdGrader` or
  `LlmJudgeGrader` where false positives matter.

## [0.1.0] — 2026-06-05

Initial release.

### Added
- Core metrics: `HitAtK`, `PrecisionAtK`, `RecallAtK`, `ReciprocalRank` (MRR),
  `AveragePrecision` (MAP), `NdcgAtK`, `F1AtK` — pure, deterministic, independently testable.
- `EvalRunner` — concurrent evaluation orchestration with retry, per-case error capture, and
  `IEvalObserver` hooks.
- Deterministic graders: `ChunkIdGrader` (id-based) and `KeywordGrader` (keyword presence).
- Reporting: `MarkdownReporter` (`RetriEval.Core`), `JsonReporter`, `HtmlReporter`,
  `RegressionComparer` (`RetriEval.Reporting`).
- Test integrations: `RetriEval.Testing.Xunit`, `RetriEval.Testing.NUnit` with `RetrievalAssert`.
- Optional add-ons: `SemanticGrader` + `IEmbedder` (`RetriEval.Embeddings.Abstractions`),
  `LlmJudgeGrader` + `ILlmClient` + `GoldenSetGenerator` (`RetriEval.Llm.Abstractions`).
- Adapters: `AzureAISearchRetriever` (`RetriEval.Adapters.AzureAISearch`),
  `QdrantRetriever` (`RetriEval.Adapters.Qdrant`).
- `retrieval-eval` CLI (`RetriEval.Cli`): `init`, `run`, `compare`, `generate` commands.
- `OpenTelemetryEvalObserver` (`RetriEval.Observability`) — exports run/case metrics as
  OTel `Meter` instruments.

[0.3.2]: https://github.com/ismailaidar/RetriEval/releases/tag/v0.3.2
[0.3.1]: https://github.com/ismailaidar/RetriEval/releases/tag/v0.3.1
[0.3.0]: https://github.com/ismailaidar/RetriEval/releases/tag/v0.3.0
[0.2.0]: https://github.com/ismailaidar/RetriEval/releases/tag/v0.2.0
[0.1.0]: https://github.com/ismailaidar/RetriEval/releases/tag/v0.1.0
