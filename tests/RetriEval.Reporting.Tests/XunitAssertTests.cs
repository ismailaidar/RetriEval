using RetriEval.Core;
using RetriEval.Testing.Xunit;
using Xunit.Sdk;

namespace RetriEval.Reporting.Tests;

public class XunitAssertTests
{
    [Fact]
    public void HitAtK_AboveThreshold_DoesNotThrow()
    {
        var report = TestReportFactory.Build(hitAtK: 0.9);
        RetrievalAssert.HitAtK(report, 0.8);
    }

    [Fact]
    public void HitAtK_BelowThreshold_Throws()
    {
        var report = TestReportFactory.Build(hitAtK: 0.5);
        Assert.Throws<XunitException>(() => RetrievalAssert.HitAtK(report, 0.8));
    }

    [Fact]
    public void Mrr_AboveThreshold_DoesNotThrow()
    {
        var report = TestReportFactory.Build(mrr: 0.7);
        RetrievalAssert.Mrr(report, 0.6);
    }

    [Fact]
    public void Mrr_BelowThreshold_Throws()
    {
        var report = TestReportFactory.Build(mrr: 0.4);
        Assert.Throws<XunitException>(() => RetrievalAssert.Mrr(report, 0.6));
    }

    [Fact]
    public void NoErrors_NoErrors_DoesNotThrow()
    {
        var report = TestReportFactory.Build(withError: false);
        RetrievalAssert.NoErrors(report);
    }

    [Fact]
    public void NoErrors_WithErrors_Throws()
    {
        var report = TestReportFactory.Build(withError: true);
        Assert.Throws<XunitException>(() => RetrievalAssert.NoErrors(report));
    }

    [Fact]
    public void FailureMessage_ContainsMetricName()
    {
        var report = TestReportFactory.Build(hitAtK: 0.1);
        var ex = Assert.Throws<XunitException>(() => RetrievalAssert.HitAtK(report, 0.9));
        Assert.Contains("Hit@", ex.Message);
    }

    [Fact]
    public void NullReport_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => RetrievalAssert.HitAtK(null!, 0.5));
    }
}
