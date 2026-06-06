using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using RetriEval.Core;

namespace RetriEval.Adapters.AzureAISearch;

/// <summary>
/// <see cref="IRetriever"/> implementation backed by Azure AI Search.
/// Supports keyword, pure-vector, and hybrid (keyword + vector) search modes.
/// </summary>
/// <remarks>
/// Configure the adapter via <see cref="AzureAISearchRetrieverOptions"/> to specify
/// field names, the vector field, and an optional query embedder for vector/hybrid search.
/// The adapter maps Azure AI Search results to <see cref="RetrievedChunk"/> using the
/// configured field names.
/// </remarks>
public sealed class AzureAISearchRetriever : IRetriever
{
    private readonly SearchClient _client;
    private readonly AzureAISearchRetrieverOptions _options;

    /// <param name="client">
    /// A configured <see cref="SearchClient"/> pointing at the target index.
    /// The index name in the client must match <see cref="AzureAISearchRetrieverOptions.IndexName"/>.
    /// </param>
    /// <param name="options">Field mapping and search mode configuration.</param>
    public AzureAISearchRetriever(SearchClient client, AzureAISearchRetrieverOptions options)
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
        var selectFields = BuildSelectList();
        var searchOptions = new SearchOptions
        {
            Size = k,
            Select = { },
        };
        foreach (var f in selectFields) searchOptions.Select.Add(f);

        // Vector / hybrid path
        if (_options.VectorField is not null && _options.QueryEmbedder is not null)
        {
            var queryVector = await _options.QueryEmbedder(query, ct).ConfigureAwait(false);
            var vectorQuery = new VectorizedQuery(queryVector)
            {
                KNearestNeighborsCount = k,
                Fields = { _options.VectorField },
            };
            searchOptions.VectorSearch = new VectorSearchOptions { Queries = { vectorQuery } };
        }

        // Keyword-only search (or hybrid: pass the query string alongside vector)
        var queryText = (_options.VectorField is null || _options.UseHybridSearch) ? query : "*";
        var response = await _client.SearchAsync<SearchDocument>(queryText, searchOptions, ct)
            .ConfigureAwait(false);

        var results = new List<RetrievedChunk>();
        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(ct).ConfigureAwait(false))
        {
            var doc = result.Document;
            var id      = GetString(doc, _options.IdField) ?? string.Empty;
            var content = GetString(doc, _options.ContentField) ?? string.Empty;
            var score   = result.Score ?? 0.0;

            Dictionary<string, string>? metadata = null;
            if (_options.MetadataFields.Count > 0)
            {
                metadata = new Dictionary<string, string>(_options.MetadataFields.Count);
                foreach (var field in _options.MetadataFields)
                {
                    var val = GetString(doc, field);
                    if (val is not null) metadata[field] = val;
                }
            }

            results.Add(new RetrievedChunk(id, content, score, metadata));
        }

        return results;
    }

    private List<string> BuildSelectList()
    {
        var fields = new List<string> { _options.IdField, _options.ContentField };
        foreach (var f in _options.MetadataFields)
        {
            if (!fields.Contains(f)) fields.Add(f);
        }
        return fields;
    }

    private static string? GetString(SearchDocument doc, string field)
    {
        if (doc.TryGetValue(field, out var val))
            return val?.ToString();
        return null;
    }
}
