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
    public async Task LoadAsync_SnakeCaseKeys_ThrowsInvalidDataExceptionNamingTheCase()
    {
        // snake_case keys silently fail to bind to RelevantChunkIds/RelevantKeywords —
        // System.Text.Json's case-insensitive matching normalizes casing, not separators.
        // The case ends up with no relevance signal at all; LoadAsync must catch this loudly.
        var path = TempFile();
        try
        {
            await File.WriteAllTextAsync(path, """
                [
                  {
                    "id": "q-humira-pa",
                    "query": "Does Humira require prior authorization?",
                    "relevant_chunk_ids": ["chunk-pa-policy"],
                    "relevant_keywords": ["Humira", "prior authorization"]
                  }
                ]
                """);

            var ex = await Assert.ThrowsAsync<InvalidDataException>(() => GoldenSetLoader.LoadAsync(path));

            Assert.Contains("q-humira-pa", ex.Message);
            Assert.Contains("camelCase", ex.Message);
            Assert.Contains("snake_case", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_CaseWithGradedRelevanceOnly_DoesNotThrow()
    {
        // GradedRelevance alone is a valid relevance signal — must not be flagged as "unlabeled".
        var path = TempFile();
        try
        {
            await File.WriteAllTextAsync(path, """
                [
                  {
                    "id": "q-graded",
                    "query": "What is the Eiffel Tower?",
                    "gradedRelevance": { "chunk-eiffel-001": 2 }
                  }
                ]
                """);

            var cases = await GoldenSetLoader.LoadAsync(path);

            var c = Assert.Single(cases);
            Assert.Equal(2, c.GradedRelevance?["chunk-eiffel-001"]);
        }
        finally
        {
            File.Delete(path);
        }
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
