namespace RetriEval.Core;

/// <summary>
/// Pure, deterministic retrieval evaluation metrics.
/// All methods are stateless and operate on simple value types so they can be unit-tested
/// independently of any retriever or grader.
/// </summary>
/// <remarks>
/// Rounding is intentionally absent from these methods. Round only at the display/report boundary.
/// All metrics return values in [0, 1] unless otherwise noted.
/// </remarks>
public static class RetrievalMetrics
{
    // -------------------------------------------------------------------------
    // Hit@k
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns 1.0 if at least one relevant item appears in the top-<paramref name="k"/> results, else 0.0.
    /// </summary>
    /// <param name="relevance">
    /// Boolean relevance flags in rank order (index 0 = rank 1). Must not be empty.
    /// </param>
    /// <param name="k">Cut-off rank. Clamped to <c>[1, relevance.Count]</c>.</param>
    public static double HitAtK(IReadOnlyList<bool> relevance, int k)
    {
        ArgumentNullException.ThrowIfNull(relevance);
        var cut = Clamp(k, 1, relevance.Count);
        for (var i = 0; i < cut; i++)
        {
            if (relevance[i]) return 1.0;
        }
        return 0.0;
    }

    // -------------------------------------------------------------------------
    // Precision@k
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the fraction of the top-<paramref name="k"/> results that are relevant.
    /// </summary>
    /// <param name="relevance">Boolean relevance flags in rank order.</param>
    /// <param name="k">Cut-off rank. Clamped to <c>[1, relevance.Count]</c>.</param>
    public static double PrecisionAtK(IReadOnlyList<bool> relevance, int k)
    {
        ArgumentNullException.ThrowIfNull(relevance);
        var cut = Clamp(k, 1, relevance.Count);
        var hits = 0;
        for (var i = 0; i < cut; i++)
        {
            if (relevance[i]) hits++;
        }
        return (double)hits / cut;
    }

    // -------------------------------------------------------------------------
    // Recall@k
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the fraction of the known-relevant set that appears in the top-<paramref name="k"/> results.
    /// </summary>
    /// <param name="relevance">Boolean relevance flags in rank order.</param>
    /// <param name="k">Cut-off rank. Clamped to <c>[1, relevance.Count]</c>.</param>
    /// <param name="totalRelevant">
    /// Total number of relevant items in the golden case (the denominator).
    /// Must be ≥ 1.
    /// </param>
    /// <remarks>
    /// <b>Known-item recall caveat:</b> this measures recall against the <em>hand-labeled</em>
    /// relevant set, not against the entire corpus. The corpus may contain unlabeled relevant
    /// chunks that this metric cannot detect. Treat Recall@k as a lower bound.
    /// </remarks>
    public static double RecallAtK(IReadOnlyList<bool> relevance, int k, int totalRelevant)
    {
        ArgumentNullException.ThrowIfNull(relevance);
        if (totalRelevant < 1) throw new ArgumentOutOfRangeException(nameof(totalRelevant), "Must be at least 1.");

        var cut = Clamp(k, 1, relevance.Count);
        var hits = 0;
        for (var i = 0; i < cut; i++)
        {
            if (relevance[i]) hits++;
        }

        // hits is a subset of the full relevant set, so totalRelevant — its size — can
        // never be smaller. A caller passing a totalRelevant that undercounts the relevant
        // set (e.g. sizing it from the wrong signal) would otherwise produce Recall@k > 1.0.
        if (hits > totalRelevant)
            throw new ArgumentOutOfRangeException(nameof(totalRelevant),
                $"totalRelevant ({totalRelevant}) is smaller than the {hits} relevant item(s) " +
                $"found in the top-{cut} alone — it must be at least that large, since those " +
                "hits are a subset of the full relevant set. Recall@k would otherwise exceed 1.0.");

        return (double)hits / totalRelevant;
    }

    // -------------------------------------------------------------------------
    // Reciprocal Rank (RR) — aggregate = MRR
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the reciprocal of the rank of the first relevant result (1/rank), or 0 if none found.
    /// Aggregate over a query set = <b>Mean Reciprocal Rank (MRR)</b>.
    /// </summary>
    /// <param name="relevance">Boolean relevance flags in rank order.</param>
    public static double ReciprocalRank(IReadOnlyList<bool> relevance)
    {
        ArgumentNullException.ThrowIfNull(relevance);
        for (var i = 0; i < relevance.Count; i++)
        {
            if (relevance[i]) return 1.0 / (i + 1);
        }
        return 0.0;
    }

    // -------------------------------------------------------------------------
    // Average Precision (AP) — aggregate = MAP
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the Average Precision: mean of Precision@i at each rank i where a relevant item appears.
    /// Returns 0 when no relevant item is found. Aggregate over a query set = <b>MAP</b>.
    /// </summary>
    /// <param name="relevance">Boolean relevance flags in rank order.</param>
    public static double AveragePrecision(IReadOnlyList<bool> relevance)
    {
        ArgumentNullException.ThrowIfNull(relevance);

        double sum = 0;
        var hits = 0;
        for (var i = 0; i < relevance.Count; i++)
        {
            if (relevance[i])
            {
                hits++;
                sum += (double)hits / (i + 1);
            }
        }
        return hits == 0 ? 0.0 : sum / hits;
    }

    // -------------------------------------------------------------------------
    // NDCG@k
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the Normalized Discounted Cumulative Gain at cut-off <paramref name="k"/>.
    /// Supports both binary gains (0/1) and graded gains (any non-negative integer).
    /// Returns 0 when the ideal DCG is 0 (no relevant items).
    /// </summary>
    /// <param name="gains">
    /// Per-rank gain values in rank order (index 0 = rank 1, gain ≥ 0).
    /// Binary relevance: pass 1 for relevant, 0 for not relevant.
    /// </param>
    /// <param name="k">Cut-off rank. Clamped to <c>[1, gains.Count]</c>.</param>
    public static double NdcgAtK(IReadOnlyList<int> gains, int k)
    {
        ArgumentNullException.ThrowIfNull(gains);
        var cut = Clamp(k, 1, gains.Count);

        var dcg = Dcg(gains, cut);
        var idealGains = gains.OrderByDescending(g => g).ToArray();
        var idcg = Dcg(idealGains, cut);

        return idcg == 0.0 ? 0.0 : dcg / idcg;
    }

    private static double Dcg(IReadOnlyList<int> gains, int cut)
    {
        double dcg = 0;
        for (var i = 0; i < cut && i < gains.Count; i++)
        {
            dcg += gains[i] / Math.Log2(i + 2); // rank = i+1, denominator = log2(rank+1)
        }
        return dcg;
    }

    // -------------------------------------------------------------------------
    // F1@k
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the harmonic mean of Precision@k and Recall@k.
    /// Returns 0 when both precision and recall are 0.
    /// </summary>
    /// <param name="relevance">Boolean relevance flags in rank order.</param>
    /// <param name="k">Cut-off rank. Clamped to <c>[1, relevance.Count]</c>.</param>
    /// <param name="totalRelevant">Total known-relevant items. Must be ≥ 1.</param>
    public static double F1AtK(IReadOnlyList<bool> relevance, int k, int totalRelevant)
    {
        var p = PrecisionAtK(relevance, k);
        var r = RecallAtK(relevance, k, totalRelevant);
        return (p + r) == 0 ? 0.0 : 2 * p * r / (p + r);
    }

    // -------------------------------------------------------------------------
    // Aggregation helpers
    // -------------------------------------------------------------------------

    /// <summary>Computes the arithmetic mean of a sequence of per-query metric values.</summary>
    /// <param name="values">Per-query metric values (e.g. one RR per query).</param>
    /// <returns>Mean value, or <see cref="double.NaN"/> when <paramref name="values"/> is empty.</returns>
    public static double Mean(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0) return double.NaN;
        double sum = 0;
        foreach (var v in values) sum += v;
        return sum / values.Count;
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;
}
