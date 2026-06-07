using RetriEval.Core;

namespace RetriEval.Core.Tests.Models;

public class GoldenSetLoaderTests
{
    [Fact]
    public async Task LoadAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => GoldenSetLoader.LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_CamelCaseJson_ParsesCaseInsensitively()
    {
        var path = TempFile();
        try
        {
            await File.WriteAllTextAsync(path, """
                [
                  {
                    "id": "q-001",
                    "query": "What is the capital of France?",
                    "relevantChunkIds": ["chunk-paris-001"],
                    "relevantKeywords": ["Paris", "capital"],
                    "category": "geography",
                    "description": "basic factual retrieval"
                  }
                ]
                """);

            var cases = await GoldenSetLoader.LoadAsync(path);

            var c = Assert.Single(cases);
            Assert.Equal("q-001", c.Id);
            Assert.Equal("What is the capital of France?", c.Query);
            Assert.Equal(["chunk-paris-001"], c.RelevantChunkIds);
            Assert.Equal(["Paris", "capital"], c.RelevantKeywords);
            Assert.Equal("geography", c.Category);
            Assert.Equal("basic factual retrieval", c.Description);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_EmptyArray_ReturnsEmptyList()
    {
        var path = TempFile();
        try
        {
            await File.WriteAllTextAsync(path, "[]");

            var cases = await GoldenSetLoader.LoadAsync(path);

            Assert.Empty(cases);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTrips()
    {
        var path = TempFile();
        try
        {
            GoldenCase[] original =
            [
                new()
                {
                    Id = "q-1",
                    Query = "Does Humira require prior authorization?",
                    RelevantChunkIds = ["chunk-pa-policy"],
                    RelevantKeywords = ["prior authorization", "Humira"],
                    GradedRelevance = new Dictionary<string, int> { ["chunk-pa-policy"] = 2 },
                    Category = "pharmacy",
                    Description = "PA lookup",
                },
            ];

            await GoldenSetLoader.SaveAsync(original, path);
            var loaded = await GoldenSetLoader.LoadAsync(path);

            var c = Assert.Single(loaded);
            Assert.Equal(original[0].Id, c.Id);
            Assert.Equal(original[0].Query, c.Query);
            Assert.Equal(original[0].RelevantChunkIds, c.RelevantChunkIds);
            Assert.Equal(original[0].RelevantKeywords, c.RelevantKeywords);
            Assert.Equal(original[0].GradedRelevance, c.GradedRelevance);
            Assert.Equal(original[0].Category, c.Category);
            Assert.Equal(original[0].Description, c.Description);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"golden-set-{Guid.NewGuid():N}.json");
}
