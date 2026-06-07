using System.Text.Json;

namespace RetriEval.Core;

/// <summary>
/// Loads and saves golden sets as JSON — the on-disk format used by the
/// <c>retrieval-eval</c> CLI (<c>init</c>/<c>run</c> commands), the samples, and the docs.
/// </summary>
/// <remarks>
/// <para>
/// The format is a JSON array of <see cref="GoldenCase"/> objects. Property <em>casing</em> is
/// matched case-insensitively, so both <c>camelCase</c> (the convention used by
/// <c>retrieval-eval init</c>) and <c>PascalCase</c> input is accepted:
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
/// <para>
/// <b>Pitfall — only casing is normalized, not separators.</b> <c>System.Text.Json</c>'s
/// case-insensitive matching maps <c>relevantChunkIds</c> ↔ <c>RelevantChunkIds</c>, but
/// <em>not</em> <c>relevant_chunk_ids</c> (snake_case) ↔ <c>RelevantChunkIds</c> — the
/// underscore makes them different property names. A snake_case key silently fails to bind
/// and the property keeps its empty-collection default, with no exception. <see cref="LoadAsync"/>
/// detects this specific failure mode — a case left with no relevance signal at all — and
/// throws <see cref="InvalidDataException"/> naming the offending case ids, rather than letting
/// you debug a run that mysteriously scores zero across the board.
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
    /// <exception cref="InvalidDataException">
    /// One or more cases deserialized with <em>no</em> relevance signal — empty
    /// <see cref="GoldenCase.RelevantChunkIds"/>, <see cref="GoldenCase.RelevantKeywords"/>,
    /// and <see cref="GoldenCase.GradedRelevance"/>. Such a case can never score above zero,
    /// and the most common cause is a property-name casing mismatch (e.g. snake_case JSON
    /// keys) that deserialized silently rather than throwing. See <b>Pitfall</b> above.
    /// </exception>
    public static async Task<IReadOnlyList<GoldenCase>> LoadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Golden set not found: {path}", path);

        await using var stream = File.OpenRead(path);
        var cases = await JsonSerializer.DeserializeAsync<List<GoldenCase>>(stream, Options, ct)
            .ConfigureAwait(false) ?? [];

        ThrowIfAnyCaseHasNoRelevanceSignal(cases, path);
        return cases;
    }

    private static void ThrowIfAnyCaseHasNoRelevanceSignal(List<GoldenCase> cases, string path)
    {
        var unlabeled = cases.Where(c =>
            c.RelevantChunkIds.Count == 0 &&
            c.RelevantKeywords.Count == 0 &&
            (c.GradedRelevance is null or { Count: 0 })
        ).Select(c => c.Id).ToList();

        if (unlabeled.Count == 0)
            return;

        throw new InvalidDataException(
            $"'{path}' contains {unlabeled.Count} of {cases.Count} golden case(s) with no " +
            $"relevance signal at all (RelevantChunkIds, RelevantKeywords, and GradedRelevance " +
            $"are all empty): {string.Join(", ", unlabeled)}. " +
            "Such cases can never score above zero. The most common cause is a JSON " +
            "property-name mismatch — GoldenSetLoader expects camelCase keys " +
            "(relevantChunkIds, relevantKeywords, gradedRelevance); keys in another " +
            "convention such as snake_case (relevant_chunk_ids) deserialize silently to " +
            "empty collections rather than throwing, because case-insensitive matching " +
            "normalizes letter casing but not separators like underscores.");
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
