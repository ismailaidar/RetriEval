using System.Text.Json;

namespace RetriEval.Core;

/// <summary>
/// Loads and saves golden sets as JSON — the on-disk format used by the
/// <c>retrieval-eval</c> CLI (<c>init</c>/<c>run</c> commands), the samples, and the docs.
/// </summary>
/// <remarks>
/// <para>
/// The format is a JSON array of <see cref="GoldenCase"/> objects. Property names are matched
/// case-insensitively, so both <c>camelCase</c> (the convention used by <c>retrieval-eval init</c>)
/// and <c>PascalCase</c> input is accepted:
/// <code>
/// [
///   {
///     "id": "q-001",
///     "query": "What is the capital of France?",
///     "relevantChunkIds": ["chunk-paris-001"],
///     "relevantKeywords": ["Paris", "capital", "France"],
///     "category": "geography",
///     "description": "Example: basic factual retrieval"
///   }
/// ]
/// </code>
/// </para>
/// </remarks>
public static class GoldenSetLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Reads a JSON array of <see cref="GoldenCase"/> objects from <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Path to the golden-set JSON file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed cases, or an empty list when the file contains <c>null</c> or an empty array.</returns>
    /// <exception cref="FileNotFoundException">No file exists at <paramref name="path"/>.</exception>
    /// <exception cref="JsonException">The file does not contain a valid JSON array of golden cases.</exception>
    public static async Task<IReadOnlyList<GoldenCase>> LoadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Golden set not found: {path}", path);

        await using var stream = File.OpenRead(path);
        var cases = await JsonSerializer.DeserializeAsync<List<GoldenCase>>(stream, Options, ct)
            .ConfigureAwait(false);
        return cases ?? [];
    }

    /// <summary>
    /// Writes <paramref name="cases"/> to <paramref name="path"/> as an indented JSON array,
    /// in the same format produced by <c>retrieval-eval init</c>.
    /// </summary>
    /// <param name="cases">The golden cases to serialize.</param>
    /// <param name="path">Destination file path. Overwrites any existing file.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(IEnumerable<GoldenCase> cases, string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, cases.ToList(), Options, ct).ConfigureAwait(false);
    }
}
