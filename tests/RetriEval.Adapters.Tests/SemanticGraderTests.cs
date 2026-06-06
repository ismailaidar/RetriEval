using RetriEval.Core;
using RetriEval.Embeddings.Abstractions;

namespace RetriEval.Adapters.Tests;

// These tests are pure unit tests — no external dependencies, always run.
public class CosineSimilarityTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        float[] v = [1f, 2f, 3f];
        Assert.Equal(1f, SemanticGrader.CosineSimilarity(v, v), 5);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsMinusOne()
    {
        float[] a = [1f, 0f];
        float[] b = [-1f, 0f];
        Assert.Equal(-1f, SemanticGrader.CosineSimilarity(a, b), 5);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        float[] a = [1f, 0f];
        float[] b = [0f, 1f];
        Assert.Equal(0f, SemanticGrader.CosineSimilarity(a, b), 5);
    }

    [Fact]
    public void CosineSimilarity_ZeroVector_ReturnsZero()
    {
        float[] a = [0f, 0f];
        float[] b = [1f, 2f];
        Assert.Equal(0f, SemanticGrader.CosineSimilarity(a, b));
    }

    [Fact]
    public void CosineSimilarity_KnownAngle_45Degrees()
    {
        // [1,0] and [1,1]/√2 → cos(45°) = √2/2 ≈ 0.7071
        float[] a = [1f, 0f];
        float[] b = [1f, 1f];
        var expected = (float)(1.0 / Math.Sqrt(2));
        Assert.Equal(expected, SemanticGrader.CosineSimilarity(a, b), 5);
    }

    [Fact]
    public void CosineSimilarity_LengthMismatch_Throws()
    {
        float[] a = [1f, 2f];
        float[] b = [1f];
        Assert.Throws<ArgumentException>(() => SemanticGrader.CosineSimilarity(a, b));
    }

    [Fact]
    public void CosineSimilarity_NullA_ThrowsArgumentNull()
    {
        float[] b = [1f];
        Assert.Throws<ArgumentNullException>(() => SemanticGrader.CosineSimilarity(null!, b));
    }

    [Fact]
    public void CosineSimilarity_NullB_ThrowsArgumentNull()
    {
        float[] a = [1f];
        Assert.Throws<ArgumentNullException>(() => SemanticGrader.CosineSimilarity(a, null!));
    }
}

public class SemanticGraderUnitTests
{
    // Uses a stub embedder — no network calls.
    [Fact]
    public async Task IsRelevantAsync_HighSimilarity_ReturnsTrue()
    {
        // Two identical vectors → cosine = 1.0
        float[] vector = [1f, 0f, 0f];
        var embedder = new StubEmbedder(_ => vector);
        var grader = new SemanticGrader(embedder, threshold: 0.90f);

        var chunk = new RetrievedChunk("c1", "Paris is the capital of France", 1.0);
        var @case = new GoldenCase { Id = "q", Query = "q", RelevantChunkIds = ["expected-chunk"] };

        var result = await grader.IsRelevantAsync(chunk, @case);
        Assert.True(result);
    }

    [Fact]
    public async Task IsRelevantAsync_LowSimilarity_ReturnsFalse()
    {
        var callCount = 0;
        // First call (chunk) → [1,0]; second call (expected) → [0,1] → cosine = 0
        var embedder = new StubEmbedder(_ =>
        {
            callCount++;
            return callCount == 1 ? new float[] { 1f, 0f } : new float[] { 0f, 1f };
        });
        var grader = new SemanticGrader(embedder, threshold: 0.80f);

        var chunk = new RetrievedChunk("c1", "Berlin is in Germany", 1.0);
        var @case = new GoldenCase { Id = "q", Query = "q", RelevantChunkIds = ["expected-chunk"] };

        var result = await grader.IsRelevantAsync(chunk, @case);
        Assert.False(result);
    }

    [Fact]
    public async Task IsRelevantAsync_NoRelevantIds_ReturnsFalse()
    {
        var embedder = new StubEmbedder(_ => new float[] { 1f, 0f });
        var grader = new SemanticGrader(embedder, threshold: 0.80f);

        var chunk = new RetrievedChunk("c1", "content", 1.0);
        var @case = new GoldenCase { Id = "q", Query = "q" }; // no RelevantChunkIds

        var result = await grader.IsRelevantAsync(chunk, @case);
        Assert.False(result);
    }

    [Fact]
    public void Constructor_ThresholdOutOfRange_Throws()
    {
        var embedder = new StubEmbedder(_ => []);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticGrader(embedder, threshold: 1.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticGrader(embedder, threshold: -0.1f));
    }
}

// ---------------------------------------------------------------------------
// Live adapter tests — skipped without credentials
// ---------------------------------------------------------------------------

/// <summary>
/// Live integration tests for <see cref="RetriEval.Adapters.AzureAISearch.AzureAISearchRetriever"/>.
/// These tests require a real Azure AI Search index and are excluded from standard CI runs.
/// Run with: dotnet test --filter "Category=Live"
/// </summary>
[Trait("Category", "Live")]
public class AzureAISearchRetrieverLiveTests
{
    private static readonly string? Endpoint  = Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT");
    private static readonly string? ApiKey    = Environment.GetEnvironmentVariable("AZURE_SEARCH_API_KEY");
    private static readonly string? IndexName = Environment.GetEnvironmentVariable("AZURE_SEARCH_INDEX");

    [SkippableFact]
    public async Task RetrieveAsync_ReturnsResults()
    {
        Skip.If(Endpoint is null || ApiKey is null || IndexName is null,
            "AZURE_SEARCH_ENDPOINT, AZURE_SEARCH_API_KEY, and AZURE_SEARCH_INDEX must be set.");

        var client = new Azure.Search.Documents.SearchClient(
            new Uri(Endpoint!),
            IndexName,
            new Azure.AzureKeyCredential(ApiKey!));

        var options = new RetriEval.Adapters.AzureAISearch.AzureAISearchRetrieverOptions
        {
            IndexName = IndexName!,
        };
        var retriever = new RetriEval.Adapters.AzureAISearch.AzureAISearchRetriever(client, options);

        var results = await retriever.RetrieveAsync("test query", k: 3);

        Assert.NotNull(results);
        Assert.True(results.Count <= 3);
    }
}

/// <summary>
/// Live integration tests for <see cref="RetriEval.Adapters.Qdrant.QdrantRetriever"/>.
/// These tests require a running Qdrant instance and are excluded from standard CI runs.
/// Run with: dotnet test --filter "Category=Live"
/// </summary>
[Trait("Category", "Live")]
public class QdrantRetrieverLiveTests
{
    private static readonly string? QdrantHost       = Environment.GetEnvironmentVariable("QDRANT_HOST");
    private static readonly string? CollectionName   = Environment.GetEnvironmentVariable("QDRANT_COLLECTION");

    [SkippableFact]
    public async Task RetrieveAsync_ReturnsResults()
    {
        Skip.If(QdrantHost is null || CollectionName is null,
            "QDRANT_HOST and QDRANT_COLLECTION must be set.");

        // Stub embedder returns a zero vector — sufficient to test connectivity.
        Task<float[]> stubEmbedder(string _, CancellationToken __) =>
            Task.FromResult(new float[768]);

        var client  = new global::Qdrant.Client.QdrantClient(QdrantHost!);
        var options = new RetriEval.Adapters.Qdrant.QdrantRetrieverOptions
        {
            CollectionName = CollectionName!,
            QueryEmbedder  = stubEmbedder,
        };
        var retriever = new RetriEval.Adapters.Qdrant.QdrantRetriever(client, options);

        var results = await retriever.RetrieveAsync("test query", k: 3);

        Assert.NotNull(results);
        Assert.True(results.Count <= 3);
    }
}

// ---------------------------------------------------------------------------
// Test double
// ---------------------------------------------------------------------------

file sealed class StubEmbedder(Func<string, float[]> embed) : IEmbedder
{
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => Task.FromResult(embed(text));
}
