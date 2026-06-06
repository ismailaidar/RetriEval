using RetriEval.Core;

namespace RetriEval.Core.Tests.Graders;

public class ChunkIdGraderTests
{
    private static RetrievedChunk Chunk(string id) => new(id, "content", 1.0);

    private static GoldenCase Case(params string[] relevantIds) => new()
    {
        Id = "test",
        Query = "q",
        RelevantChunkIds = relevantIds,
    };

    [Fact]
    public void IsRelevant_IdInList_ReturnsTrue()
    {
        var grader = ChunkIdGrader.Instance;
        Assert.True(grader.IsRelevant(Chunk("chunk-1"), Case("chunk-1", "chunk-2")));
    }

    [Fact]
    public void IsRelevant_IdNotInList_ReturnsFalse()
    {
        var grader = ChunkIdGrader.Instance;
        Assert.False(grader.IsRelevant(Chunk("chunk-99"), Case("chunk-1")));
    }

    [Fact]
    public void IsRelevant_EmptyRelevantIds_ReturnsFalse()
    {
        var grader = ChunkIdGrader.Instance;
        Assert.False(grader.IsRelevant(Chunk("chunk-1"), Case()));
    }

    [Fact]
    public void IsRelevant_CaseSensitive()
    {
        var grader = ChunkIdGrader.Instance;
        Assert.False(grader.IsRelevant(Chunk("CHUNK-1"), Case("chunk-1")));
    }

    [Fact]
    public void Gain_IdInGradedRelevance_ReturnsGain()
    {
        var grader = ChunkIdGrader.Instance;
        var goldenCase = new GoldenCase
        {
            Id = "test",
            Query = "q",
            RelevantChunkIds = ["chunk-1"],
            GradedRelevance = new Dictionary<string, int> { ["chunk-1"] = 3 },
        };
        Assert.Equal(3, grader.Gain(Chunk("chunk-1"), goldenCase));
    }

    [Fact]
    public void Gain_IdNotInGradedRelevance_FallsBackToBinary()
    {
        var grader = ChunkIdGrader.Instance;
        var goldenCase = new GoldenCase
        {
            Id = "test",
            Query = "q",
            RelevantChunkIds = ["chunk-1"],
            GradedRelevance = new Dictionary<string, int> { ["chunk-2"] = 3 },
        };
        Assert.Equal(1, grader.Gain(Chunk("chunk-1"), goldenCase)); // relevant but no graded score
    }

    [Fact]
    public void Gain_NotRelevant_ReturnsZero()
    {
        var grader = ChunkIdGrader.Instance;
        Assert.Equal(0, grader.Gain(Chunk("chunk-99"), Case("chunk-1")));
    }
}

public class KeywordGraderTests
{
    private static RetrievedChunk Chunk(string content) => new("id", content, 1.0);

    private static GoldenCase Case(params string[] keywords) => new()
    {
        Id = "test",
        Query = "q",
        RelevantKeywords = keywords,
    };

    [Fact]
    public void IsRelevant_ContentContainsKeyword_ReturnsTrue()
    {
        var grader = KeywordGrader.Instance;
        Assert.True(grader.IsRelevant(Chunk("The capital of France is Paris."), Case("Paris")));
    }

    [Fact]
    public void IsRelevant_CaseInsensitive()
    {
        var grader = KeywordGrader.Instance;
        Assert.True(grader.IsRelevant(Chunk("paris is a city"), Case("PARIS")));
    }

    [Fact]
    public void IsRelevant_ContentMissingAllKeywords_ReturnsFalse()
    {
        var grader = KeywordGrader.Instance;
        Assert.False(grader.IsRelevant(Chunk("London is in England"), Case("Paris", "France")));
    }

    [Fact]
    public void IsRelevant_AnyKeywordMatches_ReturnsTrue()
    {
        var grader = KeywordGrader.Instance;
        Assert.True(grader.IsRelevant(Chunk("Berlin is the capital"), Case("Paris", "Berlin")));
    }

    [Fact]
    public void IsRelevant_EmptyKeywords_ReturnsFalse()
    {
        var grader = KeywordGrader.Instance;
        Assert.False(grader.IsRelevant(Chunk("any content"), Case()));
    }

    [Fact]
    public void Gain_Default_BinaryOneWhenRelevant()
    {
        // Gain is a default interface method — access via IGrader reference.
        IGrader grader = KeywordGrader.Instance;
        Assert.Equal(1, grader.Gain(Chunk("paris is nice"), Case("paris")));
    }

    [Fact]
    public void Gain_Default_ZeroWhenNotRelevant()
    {
        IGrader grader = KeywordGrader.Instance;
        Assert.Equal(0, grader.Gain(Chunk("london fog"), Case("paris")));
    }
}
