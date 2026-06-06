using Google.Protobuf.Collections;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RetriEval.Core;

namespace RetriEval.Adapters.Qdrant;

/// <summary>
/// <see cref="IRetriever"/> implementation backed by Qdrant vector store.
/// </summary>
/// <remarks>
/// Each <see cref="RetrieveAsync"/> call embeds the query using
/// <see cref="QdrantRetrieverOptions.QueryEmbedder"/>, executes a nearest-neighbour search,
/// and maps payload fields to <see cref="RetrievedChunk"/> via
/// <see cref="QdrantRetrieverOptions.IdField"/> and <see cref="QdrantRetrieverOptions.ContentField"/>.
/// </remarks>
public sealed class QdrantRetriever : IRetriever
{
    private readonly QdrantClient _client;
    private readonly QdrantRetrieverOptions _options;

    /// <param name="client">A configured <see cref="QdrantClient"/>.</param>
    /// <param name="options">Collection and field mapping configuration.</param>
    public QdrantRetriever(QdrantClient client, QdrantRetrieverOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query, int k, CancellationToken ct = default)
    {
        var queryVector = await _options.QueryEmbedder(query, ct).ConfigureAwait(false);

        var searchParams = new SearchParams { HnswEf = (ulong)(k * 2) };

        IReadOnlyList<ScoredPoint> hits;
        if (_options.VectorName is { } vectorName)
        {
            hits = await _client.SearchAsync(
                _options.CollectionName,
                queryVector,
                vectorName: vectorName,
                limit: (ulong)k,
                scoreThreshold: _options.ScoreThreshold,
                searchParams: searchParams,
                cancellationToken: ct).ConfigureAwait(false);
        }
        else
        {
            hits = await _client.SearchAsync(
                _options.CollectionName,
                queryVector,
                limit: (ulong)k,
                scoreThreshold: _options.ScoreThreshold,
                searchParams: searchParams,
                cancellationToken: ct).ConfigureAwait(false);
        }

        var results = new List<RetrievedChunk>(hits.Count);
        foreach (var hit in hits)
        {
            var id      = GetPayloadString(hit.Payload, _options.IdField) ?? hit.Id.ToString();
            var content = GetPayloadString(hit.Payload, _options.ContentField) ?? string.Empty;
            var score   = (double)hit.Score;

            Dictionary<string, string>? metadata = null;
            if (_options.MetadataFields.Count > 0)
            {
                metadata = new Dictionary<string, string>(_options.MetadataFields.Count);
                foreach (var field in _options.MetadataFields)
                {
                    var val = GetPayloadString(hit.Payload, field);
                    if (val is not null) metadata[field] = val;
                }
            }

            results.Add(new RetrievedChunk(id, content, score, metadata));
        }

        return results;
    }

    private static string? GetPayloadString(MapField<string, Value> payload, string key)
    {
        if (payload.TryGetValue(key, out var value))
        {
            return value.KindCase switch
            {
                Value.KindOneofCase.StringValue => value.StringValue,
                Value.KindOneofCase.IntegerValue => value.IntegerValue.ToString(),
                Value.KindOneofCase.DoubleValue  => value.DoubleValue.ToString(),
                _ => null,
            };
        }
        return null;
    }
}
