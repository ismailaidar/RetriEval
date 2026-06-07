using RetriEval.Core;

namespace RetriEval.Core.Tests.Metrics;

public class HitAtKTests
{
    [Fact]
    public void HitAtK_NoRelevant_ReturnsZero()
    {
        bool[] rel = [false, false, false];
        Assert.Equal(0.0, RetrievalMetrics.HitAtK(rel, 3));
    }

    [Fact]
    public void HitAtK_FirstRelevant_ReturnsOne()
    {
        bool[] rel = [true, false, false];
        Assert.Equal(1.0, RetrievalMetrics.HitAtK(rel, 3));
    }

    [Fact]
    public void HitAtK_LastRelevant_ReturnsOne()
    {
        bool[] rel = [false, false, true];
        Assert.Equal(1.0, RetrievalMetrics.HitAtK(rel, 3));
    }

    [Fact]
    public void HitAtK_RelevantBeyondCutoff_ReturnsZero()
    {
        // k=2, relevant only at position 3
        bool[] rel = [false, false, true];
        Assert.Equal(0.0, RetrievalMetrics.HitAtK(rel, 2));
    }

    [Fact]
    public void HitAtK_KLargerThanList_ClampsToListSize()
    {
        bool[] rel = [false, true];
        Assert.Equal(1.0, RetrievalMetrics.HitAtK(rel, 100));
    }

    [Fact]
    public void HitAtK_KOne_OnlyFirstPosition()
    {
        bool[] rel = [false, true, true];
        Assert.Equal(0.0, RetrievalMetrics.HitAtK(rel, 1));
    }

    [Theory]
    [InlineData(new[] { false, true, false }, 3, 1.0)]
    [InlineData(new[] { false, false, false }, 3, 0.0)]
    [InlineData(new[] { true, false, false }, 1, 1.0)]
    [InlineData(new[] { false, true, false }, 1, 0.0)]
    public void HitAtK_KnownInputsProduceKnownOutputs(bool[] rel, int k, double expected)
    {
        Assert.Equal(expected, RetrievalMetrics.HitAtK(rel, k));
    }
}

public class PrecisionAtKTests
{
    [Fact]
    public void PrecisionAtK_AllRelevant_ReturnsOne()
    {
        bool[] rel = [true, true, true];
        Assert.Equal(1.0, RetrievalMetrics.PrecisionAtK(rel, 3));
    }

    [Fact]
    public void PrecisionAtK_NoneRelevant_ReturnsZero()
    {
        bool[] rel = [false, false, false];
        Assert.Equal(0.0, RetrievalMetrics.PrecisionAtK(rel, 3));
    }

    [Fact]
    public void PrecisionAtK_OneOfThree_ReturnsOneThird()
    {
        bool[] rel = [false, true, false];
        Assert.Equal(1.0 / 3, RetrievalMetrics.PrecisionAtK(rel, 3), 10);
    }

    [Fact]
    public void PrecisionAtK_TwoOfThree_ReturnsTwoThirds()
    {
        bool[] rel = [true, false, true];
        Assert.Equal(2.0 / 3, RetrievalMetrics.PrecisionAtK(rel, 3), 10);
    }

    [Fact]
    public void PrecisionAtK_CutoffIgnoresTrailing()
    {
        // k=2: only look at first 2; both false → 0
        bool[] rel = [false, false, true];
        Assert.Equal(0.0, RetrievalMetrics.PrecisionAtK(rel, 2));
    }

    [Fact]
    public void PrecisionAtK_IsInUnitInterval()
    {
        bool[] rel = [true, false, true, false, true];
        for (var k = 1; k <= 5; k++)
        {
            var p = RetrievalMetrics.PrecisionAtK(rel, k);
            Assert.InRange(p, 0.0, 1.0);
        }
    }
}

public class RecallAtKTests
{
    [Fact]
    public void RecallAtK_AllRelevantRetrieved_ReturnsOne()
    {
        bool[] rel = [true, true];
        Assert.Equal(1.0, RetrievalMetrics.RecallAtK(rel, 2, 2));
    }

    [Fact]
    public void RecallAtK_NoneRetrieved_ReturnsZero()
    {
        bool[] rel = [false, false];
        Assert.Equal(0.0, RetrievalMetrics.RecallAtK(rel, 2, 3));
    }

    [Fact]
    public void RecallAtK_PartialRetrieval()
    {
        bool[] rel = [true, false, false];
        Assert.Equal(1.0 / 3, RetrievalMetrics.RecallAtK(rel, 3, 3), 10);
    }

    [Fact]
    public void RecallAtK_IsNonDecreasingInK()
    {
        // More results can never hurt recall
        bool[] rel = [true, false, true, false, true];
        double prev = 0;
        for (var k = 1; k <= 5; k++)
        {
            var r = RetrievalMetrics.RecallAtK(rel, k, 3);
            Assert.True(r >= prev - 1e-12, $"Recall decreased at k={k}: {r} < {prev}");
            prev = r;
        }
    }

    [Fact]
    public void RecallAtK_TotalRelevantZero_ThrowsArgumentOutOfRange()
    {
        bool[] rel = [true];
        Assert.Throws<ArgumentOutOfRangeException>(() => RetrievalMetrics.RecallAtK(rel, 1, 0));
    }

    [Fact]
    public void RecallAtK_IsInUnitInterval()
    {
        bool[] rel = [true, true, false];
        for (var k = 1; k <= 3; k++)
        {
            var r = RetrievalMetrics.RecallAtK(rel, k, 3);
            Assert.InRange(r, 0.0, 1.0);
        }
    }
}

public class ReciprocalRankTests
{
    [Fact]
    public void RR_FirstRelevant_ReturnsOne()
    {
        bool[] rel = [true, false, false];
        Assert.Equal(1.0, RetrievalMetrics.ReciprocalRank(rel));
    }

    [Fact]
    public void RR_SecondRelevant_ReturnsHalf()
    {
        bool[] rel = [false, true, false];
        Assert.Equal(0.5, RetrievalMetrics.ReciprocalRank(rel));
    }

    [Fact]
    public void RR_ThirdRelevant_ReturnsOneThird()
    {
        bool[] rel = [false, false, true];
        Assert.Equal(1.0 / 3, RetrievalMetrics.ReciprocalRank(rel), 10);
    }

    [Fact]
    public void RR_NoRelevant_ReturnsZero()
    {
        bool[] rel = [false, false, false];
        Assert.Equal(0.0, RetrievalMetrics.ReciprocalRank(rel));
    }

    [Fact]
    public void RR_AdditionalRelevantAfterFirstDoesNotChangeResult()
    {
        // Adding more relevant items after the first relevant does not change RR
        bool[] withExtra = [false, true, true];
        bool[] withoutExtra = [false, true, false];
        Assert.Equal(
            RetrievalMetrics.ReciprocalRank(withoutExtra),
            RetrievalMetrics.ReciprocalRank(withExtra));
    }
}

public class AveragePrecisionTests
{
    [Fact]
    public void AP_AllRelevant_ReturnsOne()
    {
        bool[] rel = [true, true, true];
        Assert.Equal(1.0, RetrievalMetrics.AveragePrecision(rel));
    }

    [Fact]
    public void AP_NoneRelevant_ReturnsZero()
    {
        bool[] rel = [false, false, false];
        Assert.Equal(0.0, RetrievalMetrics.AveragePrecision(rel));
    }

    [Fact]
    public void AP_KnownVector_FalseTrueFalse()
    {
        // Relevant at rank 2: P@2 = 1/2. AP = (1/2) / 1 = 0.5
        bool[] rel = [false, true, false];
        Assert.Equal(0.5, RetrievalMetrics.AveragePrecision(rel), 10);
    }

    [Fact]
    public void AP_TwoRelevantAtRankOneAndThree()
    {
        // P@1 = 1/1 = 1.0 (rank 1 relevant)
        // P@3 = 2/3 (rank 3 relevant)
        // AP = (1.0 + 2.0/3) / 2 = (1.0 + 0.6667) / 2 = 0.8333
        bool[] rel = [true, false, true];
        var expected = (1.0 + 2.0 / 3) / 2;
        Assert.Equal(expected, RetrievalMetrics.AveragePrecision(rel), 10);
    }

    [Fact]
    public void AP_IsInUnitInterval()
    {
        bool[] rel = [true, false, true, false];
        var ap = RetrievalMetrics.AveragePrecision(rel);
        Assert.InRange(ap, 0.0, 1.0);
    }
}

public class NdcgAtKTests
{
    [Fact]
    public void Ndcg_PerfectRanking_ReturnsOne()
    {
        // All relevant, already in ideal order
        int[] gains = [1, 1, 1];
        Assert.Equal(1.0, RetrievalMetrics.NdcgAtK(gains, 3), 10);
    }

    [Fact]
    public void Ndcg_NoRelevant_ReturnsZero()
    {
        int[] gains = [0, 0, 0];
        Assert.Equal(0.0, RetrievalMetrics.NdcgAtK(gains, 3));
    }

    [Fact]
    public void Ndcg_BinaryRelevant_AtRank2()
    {
        // DCG@3: gain[0]=0, gain[1]=1/log2(3), gain[2]=0
        // IDCG@3: 1/log2(2) = 1.0 (ideal: relevant at rank 1)
        int[] gains = [0, 1, 0];
        var dcg = 1.0 / Math.Log2(3);
        var idcg = 1.0 / Math.Log2(2);
        Assert.Equal(dcg / idcg, RetrievalMetrics.NdcgAtK(gains, 3), 10);
    }

    [Fact]
    public void Ndcg_GradedRelevance()
    {
        // gains: 3, 2, 3 → DCG = 3/log2(2) + 2/log2(3) + 3/log2(4)
        // ideal: 3, 3, 2 → IDCG = 3/log2(2) + 3/log2(3) + 2/log2(4)
        int[] gains = [3, 2, 3];
        var dcg = 3.0 / Math.Log2(2) + 2.0 / Math.Log2(3) + 3.0 / Math.Log2(4);
        var idcg = 3.0 / Math.Log2(2) + 3.0 / Math.Log2(3) + 2.0 / Math.Log2(4);
        Assert.Equal(dcg / idcg, RetrievalMetrics.NdcgAtK(gains, 3), 10);
    }

    [Fact]
    public void Ndcg_CutAtK_IgnoresRemainder()
    {
        // gains=[1,0,1], k=2: DCG@2 = 1/log2(2).
        // IDCG@2 sorts all gains → [1,1,0] → 1/log2(2) + 1/log2(3).
        // The rank-3 gain=1 still contributes to IDCG even though it's beyond the k cut in DCG.
        int[] gains = [1, 0, 1];
        var dcg = 1.0 / Math.Log2(2);
        var idcg = 1.0 / Math.Log2(2) + 1.0 / Math.Log2(3);
        Assert.Equal(dcg / idcg, RetrievalMetrics.NdcgAtK(gains, 2), 10);
    }

    [Fact]
    public void Ndcg_PerfectTop2Ranking_ReturnsOne()
    {
        // Both relevant items are in the top-2; rank-3 is irrelevant → NDCG@2 = 1
        int[] gains = [1, 1, 0];
        Assert.Equal(1.0, RetrievalMetrics.NdcgAtK(gains, 2), 10);
    }

    [Fact]
    public void Ndcg_IsInUnitInterval()
    {
        int[] gains = [2, 0, 1, 3];
        for (var k = 1; k <= 4; k++)
        {
            var n = RetrievalMetrics.NdcgAtK(gains, k);
            Assert.InRange(n, 0.0, 1.0 + 1e-10);
        }
    }
}

public class F1AtKTests
{
    [Fact]
    public void F1AtK_PerfectRetrieval_ReturnsOne()
    {
        bool[] rel = [true, true];
        Assert.Equal(1.0, RetrievalMetrics.F1AtK(rel, 2, 2), 10);
    }

    [Fact]
    public void F1AtK_NoPrecisionNoRecall_ReturnsZero()
    {
        bool[] rel = [false, false];
        Assert.Equal(0.0, RetrievalMetrics.F1AtK(rel, 2, 3));
    }

    [Fact]
    public void F1AtK_IsHarmonicMeanOfPrecisionAndRecall()
    {
        bool[] rel = [true, false, false];
        var p = RetrievalMetrics.PrecisionAtK(rel, 3);
        var r = RetrievalMetrics.RecallAtK(rel, 3, 2);
        var expected = 2 * p * r / (p + r);
        Assert.Equal(expected, RetrievalMetrics.F1AtK(rel, 3, 2), 10);
    }

    [Fact]
    public void F1AtK_IsInUnitInterval()
    {
        // F1 is the harmonic mean of two [0,1] ratios (Precision@k, Recall@k) and can
        // therefore never exceed 1.0 for any *valid* totalRelevant — i.e. one no smaller
        // than the relevant items actually present in `rel` (see CHANGELOG 0.3.2 for a
        // real-world case where an inconsistent totalRelevant computed upstream let
        // Recall@k — and so F1@k — exceed 1.0).
        bool[] rel = [true, true, false];
        for (var totalRelevant = 2; totalRelevant <= 4; totalRelevant++)
        {
            for (var k = 1; k <= 3; k++)
            {
                var f1 = RetrievalMetrics.F1AtK(rel, k, totalRelevant);
                Assert.InRange(f1, 0.0, 1.0);
            }
        }
    }

    [Fact]
    public void RecallAtK_TotalRelevantSmallerThanObservedHits_ThrowsArgumentOutOfRange()
    {
        // totalRelevant must be at least the number of relevant items found, since those
        // hits are necessarily a subset of the full relevant set. A smaller totalRelevant
        // is an inconsistent input that would silently produce Recall@k > 1.0 — guard
        // against it loudly instead (see CHANGELOG 0.3.2).
        bool[] rel = [true, true, true];

        Assert.Throws<ArgumentOutOfRangeException>(() => RetrievalMetrics.RecallAtK(rel, 3, 1));
    }
}

public class MeanTests
{
    [Fact]
    public void Mean_EmptyList_ReturnsNaN()
    {
        Assert.True(double.IsNaN(RetrievalMetrics.Mean([])));
    }

    [Fact]
    public void Mean_SingleValue_ReturnsThatValue()
    {
        Assert.Equal(0.75, RetrievalMetrics.Mean([0.75]));
    }

    [Fact]
    public void Mean_KnownValues()
    {
        double[] values = [0.0, 0.5, 1.0];
        Assert.Equal(0.5, RetrievalMetrics.Mean(values), 10);
    }
}
