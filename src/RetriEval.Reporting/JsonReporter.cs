using System.Text.Json;
using System.Text.Json.Serialization;
using RetriEval.Core;

namespace RetriEval.Reporting;

/// <summary>
/// Serializes an <see cref="EvalReport"/> to structured JSON.
/// Useful for programmatic processing, custom dashboards, or archiving run history.
/// </summary>
public sealed class JsonReporter
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly JsonSerializerOptions _options;

    /// <param name="options">
    /// Custom serializer options. When <see langword="null"/>, pretty-printed JSON with
    /// null-suppression is used.
    /// </param>
    public JsonReporter(JsonSerializerOptions? options = null)
    {
        _options = options ?? DefaultOptions;
    }

    /// <summary>Serializes the report to a JSON string.</summary>
    public string Render(EvalReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(ToDto(report), _options);
    }

    /// <summary>Writes the JSON to <paramref name="path"/>, creating or overwriting the file.</summary>
    public async Task WriteAsync(EvalReport report, string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, ToDto(report), _options, ct).ConfigureAwait(false);
    }

    /// <summary>Deserializes a JSON file written by <see cref="WriteAsync"/> back to a DTO.</summary>
    public static async Task<EvalReportDto> ReadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var fs = File.OpenRead(path);
        var dto = await JsonSerializer.DeserializeAsync<EvalReportDto>(fs, DefaultOptions, ct).ConfigureAwait(false);
        return dto ?? throw new JsonException($"Failed to deserialize report from '{path}'.");
    }

    private static EvalReportDto ToDto(EvalReport report) => new()
    {
        RunAt = report.RunAt,
        RetrieverName = report.RetrieverName,
        GraderName = report.GraderName,
        K = report.Options.K,
        Aggregate = report.Aggregate,
        Results = report.Results.Select(r => new QueryResultDto
        {
            CaseId = r.CaseId,
            Query = r.Query,
            Category = r.Category,
            Error = r.Error?.Message,
            Retrieved = r.Retrieved.Select(c => new ChunkDto
            {
                Id = c.Id,
                Score = c.Score,
                Metadata = c.Metadata,
            }).ToList(),
            Relevance = [.. r.Relevance],
            Gains = [.. r.Gains],
            Metrics = r.Metrics,
        }).ToList(),
    };
}

// ---------------------------------------------------------------------------
// DTOs — plain data shapes for JSON serialization
// ---------------------------------------------------------------------------

/// <summary>JSON-serializable representation of an <see cref="EvalReport"/>.</summary>
public sealed class EvalReportDto
{
    /// <inheritdoc cref="EvalReport.RunAt"/>
    public DateTimeOffset RunAt { get; set; }

    /// <inheritdoc cref="EvalReport.RetrieverName"/>
    public string RetrieverName { get; set; } = "";

    /// <inheritdoc cref="EvalReport.GraderName"/>
    public string GraderName { get; set; } = "";

    /// <summary>The k value used for all cut-off metrics.</summary>
    public int K { get; set; }

    /// <inheritdoc cref="EvalReport.Aggregate"/>
    public AggregateMetrics? Aggregate { get; set; }

    /// <inheritdoc cref="EvalReport.Results"/>
    public List<QueryResultDto> Results { get; set; } = [];
}

/// <summary>JSON-serializable representation of a <see cref="QueryResult"/>.</summary>
public sealed class QueryResultDto
{
    /// <inheritdoc cref="QueryResult.CaseId"/>
    public string CaseId { get; set; } = "";

    /// <inheritdoc cref="QueryResult.Query"/>
    public string Query { get; set; } = "";

    /// <inheritdoc cref="QueryResult.Category"/>
    public string? Category { get; set; }

    /// <summary>Error message if the retriever threw; <see langword="null"/> on success.</summary>
    public string? Error { get; set; }

    /// <summary>Slim chunk representations (id + score + metadata only).</summary>
    public List<ChunkDto> Retrieved { get; set; } = [];

    /// <inheritdoc cref="QueryResult.Relevance"/>
    public bool[] Relevance { get; set; } = [];

    /// <inheritdoc cref="QueryResult.Gains"/>
    public int[] Gains { get; set; } = [];

    /// <inheritdoc cref="QueryResult.Metrics"/>
    public QueryMetrics? Metrics { get; set; }
}

/// <summary>Slim JSON representation of a <see cref="RetrievedChunk"/>.</summary>
public sealed class ChunkDto
{
    /// <inheritdoc cref="RetrievedChunk.Id"/>
    public string Id { get; set; } = "";

    /// <inheritdoc cref="RetrievedChunk.Score"/>
    public double Score { get; set; }

    /// <inheritdoc cref="RetrievedChunk.Metadata"/>
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}
