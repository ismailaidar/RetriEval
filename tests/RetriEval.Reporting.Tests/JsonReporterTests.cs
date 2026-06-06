using RetriEval.Core;
using RetriEval.Reporting;

namespace RetriEval.Reporting.Tests;

public class JsonReporterTests
{
    private static EvalReport SampleReport() => TestReportFactory.Build();

    [Fact]
    public void Render_ProducesValidJson()
    {
        var reporter = new JsonReporter();
        var json = reporter.Render(SampleReport());
        Assert.False(string.IsNullOrWhiteSpace(json));
        // Basic structure check — would throw on malformed JSON
        var dto = System.Text.Json.JsonSerializer.Deserialize<EvalReportDto>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(dto);
    }

    [Fact]
    public void Render_ContainsRetrieverName()
    {
        var json = new JsonReporter().Render(SampleReport());
        Assert.Contains("TestRetriever", json);
    }

    [Fact]
    public void Render_ContainsCaseId()
    {
        var json = new JsonReporter().Render(SampleReport());
        Assert.Contains("case-1", json);
    }

    [Fact]
    public async Task WriteAsync_RoundTrips_Via_ReadAsync()
    {
        var reporter = new JsonReporter();
        var path = Path.GetTempFileName();
        try
        {
            await reporter.WriteAsync(SampleReport(), path);
            var dto = await JsonReporter.ReadAsync(path);
            Assert.Equal("TestRetriever", dto.RetrieverName);
            Assert.Single(dto.Results);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Render_NullReport_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonReporter().Render(null!));
    }
}
