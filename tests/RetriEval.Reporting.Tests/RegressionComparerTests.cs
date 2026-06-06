using RetriEval.Core;
using RetriEval.Reporting;

namespace RetriEval.Reporting.Tests;

public class RegressionComparerTests
{
    private static readonly RegressionComparer Comparer = new();

    [Fact]
    public void Compare_IdenticalReports_NoRegressions()
    {
        var report = TestReportFactory.Build();
        var result = Comparer.Compare(report, report);
        Assert.False(result.HasRegressions);
        Assert.Empty(result.RegressedCases);
    }

    [Fact]
    public void Compare_NdcgDropped_DetectsRegression()
    {
        var baseline = TestReportFactory.Build(ndcg: 1.0);
        var current  = TestReportFactory.Build(ndcg: 0.3);
        var result   = Comparer.Compare(baseline, current);
        Assert.True(result.HasRegressions);
        Assert.Single(result.RegressedCases);
    }

    [Fact]
    public void Compare_NdcgImproved_DetectsImprovement()
    {
        var baseline = TestReportFactory.Build(ndcg: 0.4);
        var current  = TestReportFactory.Build(ndcg: 0.9);
        var result   = Comparer.Compare(baseline, current);
        Assert.Empty(result.RegressedCases);
        Assert.Single(result.ImprovedCases);
    }

    [Fact]
    public void Compare_NewCaseInCurrent_MarkedAsAdded()
    {
        var baseline = TestReportFactory.Build(caseId: "old");
        var current  = TestReportFactory.Build(caseId: "new");
        var result   = Comparer.Compare(baseline, current);
        var added = result.QueryDeltas.First(d => d.CaseId == "new");
        Assert.Equal(DeltaStatus.Added, added.Status);
    }

    [Fact]
    public void Compare_CaseRemovedFromCurrent_MarkedAsRemoved()
    {
        var baseline = TestReportFactory.Build(caseId: "old");
        var current  = TestReportFactory.Build(caseId: "new");
        var result   = Comparer.Compare(baseline, current);
        var removed = result.QueryDeltas.First(d => d.CaseId == "old");
        Assert.Equal(DeltaStatus.Removed, removed.Status);
    }

    [Fact]
    public void Compare_ErroredCase_MarkedAsErrored()
    {
        // Build baseline with "case-1" succeeding; current with "case-1" errored.
        var baseline = TestReportFactory.Build(caseId: "case-1");
        var current  = TestReportFactory.BuildError(caseId: "case-1");
        var result   = Comparer.Compare(baseline, current);
        var errored  = result.QueryDeltas.FirstOrDefault(d => d.Status == DeltaStatus.Errored);
        Assert.NotNull(errored);
        Assert.Equal("case-1", errored.CaseId);
    }

    [Fact]
    public void Compare_AggregateDelta_IsCorrect()
    {
        var baseline = TestReportFactory.Build(mrr: 0.5);
        var current  = TestReportFactory.Build(mrr: 0.8);
        var result   = Comparer.Compare(baseline, current);
        Assert.Equal(0.3, result.AggregateDelta.Mrr, 6);
    }

    [Fact]
    public void Compare_NullBaseline_ThrowsArgumentNull()
    {
        var r = TestReportFactory.Build();
        Assert.Throws<ArgumentNullException>(() => Comparer.Compare(null!, r));
    }

    [Fact]
    public void Compare_NullCurrent_ThrowsArgumentNull()
    {
        var r = TestReportFactory.Build();
        Assert.Throws<ArgumentNullException>(() => Comparer.Compare(r, null!));
    }
}
