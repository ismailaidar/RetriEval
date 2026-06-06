using System.Diagnostics;
using System.Diagnostics.Metrics;
using RetriEval.Core;

namespace RetriEval.Observability;

/// <summary>
/// An <see cref="IEvalObserver"/> that exports retrieval evaluation metrics as
/// OpenTelemetry instruments (Meter/Counter/Histogram).
/// </summary>
/// <remarks>
/// <para>
/// Register the <c>RetriEval</c> meter in your OTel SDK configuration to collect these metrics:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(m => m.AddMeter("RetriEval"));
/// </code>
/// </para>
/// <para>
/// Instruments emitted:
/// <list type="table">
///   <listheader><term>Instrument</term><description>Description</description></listheader>
///   <item><term>retrieval_eval.case.duration_ms</term><description>Histogram of per-case evaluation time (ms).</description></item>
///   <item><term>retrieval_eval.case.error</term><description>Counter of errored cases.</description></item>
///   <item><term>retrieval_eval.hit_at_k</term><description>Histogram of per-case Hit@k values.</description></item>
///   <item><term>retrieval_eval.reciprocal_rank</term><description>Histogram of per-case Reciprocal Rank values.</description></item>
///   <item><term>retrieval_eval.ndcg_at_k</term><description>Histogram of per-case NDCG@k values.</description></item>
///   <item><term>retrieval_eval.run.mrr</term><description>Gauge of the run-level MRR (emitted once per run).</description></item>
///   <item><term>retrieval_eval.run.map</term><description>Gauge of the run-level MAP (emitted once per run).</description></item>
///   <item><term>retrieval_eval.run.hit_at_k</term><description>Gauge of the run-level mean Hit@k.</description></item>
///   <item><term>retrieval_eval.run.ndcg_at_k</term><description>Gauge of the run-level mean NDCG@k.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class OpenTelemetryEvalObserver : IEvalObserver, IDisposable
{
    /// <summary>The OTel meter name. Use this when configuring AddMeter in your OTel SDK setup.</summary>
    public const string MeterName = "RetriEval";

    private readonly Meter _meter;
    private readonly Histogram<double> _caseDurationMs;
    private readonly Counter<long>     _caseErrorCount;
    private readonly Histogram<double> _hitAtK;
    private readonly Histogram<double> _reciprocalRank;
    private readonly Histogram<double> _ndcgAtK;
    private readonly ObservableGauge<double> _runMrr;
    private readonly ObservableGauge<double> _runMap;
    private readonly ObservableGauge<double> _runHitAtK;
    private readonly ObservableGauge<double> _runNdcgAtK;

    private readonly Dictionary<string, long> _caseStartTimes = new();
    private readonly object _lock = new();

    // Latest run-level values for the gauges
    private double _lastMrr;
    private double _lastMap;
    private double _lastRunHitAtK;
    private double _lastRunNdcgAtK;

    /// <param name="meterVersion">Optional version string for the meter (defaults to "1.0.0").</param>
    public OpenTelemetryEvalObserver(string? meterVersion = null)
    {
        _meter = new Meter(MeterName, meterVersion ?? "1.0.0");

        _caseDurationMs = _meter.CreateHistogram<double>(
            "retrieval_eval.case.duration_ms", "ms", "Time taken to evaluate a single golden case.");
        _caseErrorCount = _meter.CreateCounter<long>(
            "retrieval_eval.case.error", description: "Number of golden cases that errored during retrieval.");
        _hitAtK = _meter.CreateHistogram<double>(
            "retrieval_eval.hit_at_k", description: "Per-case Hit@k (1 = at least one relevant result found).");
        _reciprocalRank = _meter.CreateHistogram<double>(
            "retrieval_eval.reciprocal_rank", description: "Per-case Reciprocal Rank (1/rank of first relevant).");
        _ndcgAtK = _meter.CreateHistogram<double>(
            "retrieval_eval.ndcg_at_k", description: "Per-case NDCG@k.");

        _runMrr     = _meter.CreateObservableGauge("retrieval_eval.run.mrr",     () => _lastMrr,      description: "Mean Reciprocal Rank for the last completed run.");
        _runMap     = _meter.CreateObservableGauge("retrieval_eval.run.map",     () => _lastMap,      description: "Mean Average Precision for the last completed run.");
        _runHitAtK  = _meter.CreateObservableGauge("retrieval_eval.run.hit_at_k",() => _lastRunHitAtK, description: "Mean Hit@k for the last completed run.");
        _runNdcgAtK = _meter.CreateObservableGauge("retrieval_eval.run.ndcg_at_k",() => _lastRunNdcgAtK, description: "Mean NDCG@k for the last completed run.");
    }

    /// <inheritdoc />
    public void OnCaseStarted(GoldenCase goldenCase)
    {
        lock (_lock)
            _caseStartTimes[goldenCase.Id] = Stopwatch.GetTimestamp();
    }

    /// <inheritdoc />
    public void OnCaseCompleted(QueryResult result)
    {
        double durationMs = 0;
        lock (_lock)
        {
            if (_caseStartTimes.TryGetValue(result.CaseId, out var start))
            {
                durationMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                _caseStartTimes.Remove(result.CaseId);
            }
        }

        var tags = new TagList { { "case_id", result.CaseId }, { "category", result.Category ?? "unknown" } };

        if (result.Error is not null)
        {
            _caseErrorCount.Add(1, tags);
        }
        else
        {
            _caseDurationMs.Record(durationMs, tags);
            _hitAtK.Record(result.Metrics.HitAtK, tags);
            _reciprocalRank.Record(result.Metrics.ReciprocalRank, tags);
            _ndcgAtK.Record(result.Metrics.NdcgAtK, tags);
        }
    }

    /// <inheritdoc />
    public void OnRunCompleted(EvalReport report)
    {
        _lastMrr       = double.IsNaN(report.Aggregate.Mrr)          ? 0 : report.Aggregate.Mrr;
        _lastMap       = double.IsNaN(report.Aggregate.Map)          ? 0 : report.Aggregate.Map;
        _lastRunHitAtK = double.IsNaN(report.Aggregate.HitAtK)       ? 0 : report.Aggregate.HitAtK;
        _lastRunNdcgAtK= double.IsNaN(report.Aggregate.MeanNdcgAtK)  ? 0 : report.Aggregate.MeanNdcgAtK;
    }

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
