# Changelog

All notable changes to RetriEval are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [SemVer](https://semver.org/).

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

[0.3.0]: https://github.com/ismailaidar/RetriEval/releases/tag/v0.3.0
[0.2.0]: https://github.com/ismailaidar/RetriEval/releases/tag/v0.2.0
[0.1.0]: https://github.com/ismailaidar/RetriEval/releases/tag/v0.1.0
