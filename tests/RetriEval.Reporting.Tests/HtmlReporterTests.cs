using RetriEval.Core;
using RetriEval.Reporting;

namespace RetriEval.Reporting.Tests;

public class HtmlReporterTests
{
    [Fact]
    public void Render_ProducesHtmlDocument()
    {
        var html = new HtmlReporter().Render(TestReportFactory.Build());
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("</html>", html);
    }

    [Fact]
    public void Render_ContainsRetrieverName()
    {
        var html = new HtmlReporter().Render(TestReportFactory.Build());
        Assert.Contains("TestRetriever", html);
    }

    [Fact]
    public void Render_ContainsCaseId()
    {
        var html = new HtmlReporter().Render(TestReportFactory.Build());
        Assert.Contains("case-1", html);
    }

    [Fact]
    public void Render_EscapesHtmlInQueryText()
    {
        var report = TestReportFactory.Build(query: "<script>alert(1)</script>");
        var html = new HtmlReporter().Render(report);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Render_WithError_ShowsErrorCard()
    {
        var report = TestReportFactory.Build(withError: true);
        var html = new HtmlReporter().Render(report);
        Assert.Contains("card error", html);
    }

    [Fact]
    public async Task WriteAsync_CreatesFile()
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".html");
        try
        {
            await new HtmlReporter().WriteAsync(TestReportFactory.Build(), path);
            Assert.True(File.Exists(path));
            var content = await File.ReadAllTextAsync(path);
            Assert.Contains("<!DOCTYPE html>", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Render_NullReport_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new HtmlReporter().Render(null!));
    }
}
