using System.Text.Json;
using RetriEval.Core;

namespace RetriEval.Llm.Abstractions;

/// <summary>
/// Synthesises candidate <see cref="GoldenCase"/> entries from a corpus by asking an LLM
/// to generate plausible queries for each chunk.
/// </summary>
/// <remarks>
/// <para>
/// <b>Output requires human review.</b> Generated cases are starting points, not ground truth.
/// A human must review, edit, and approve each case before adding it to the golden set.
/// The generator deliberately marks all output with <see cref="GoldenCase.Description"/>
/// containing <c>"[GENERATED — needs human review]"</c> so unreviewed cases are obvious.
/// </para>
/// <para>
/// <b>Non-deterministic and costly.</b> Each chunk triggers one LLM call.
/// For a corpus of N chunks this costs N API calls. Batch carefully.
/// </para>
/// </remarks>
public class GoldenSetGenerator
{
    private readonly ILlmClient _llm;

    /// <param name="llm">LLM client used to generate queries.</param>
    public GoldenSetGenerator(ILlmClient llm)
    {
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
    }

    /// <summary>
    /// Generates candidate golden cases from <paramref name="chunks"/>.
    /// One case is produced per chunk (or fewer if the LLM skips a chunk).
    /// </summary>
    /// <param name="chunks">Corpus chunks to generate queries for.</param>
    /// <param name="queriesPerChunk">
    /// Number of distinct queries to generate per chunk (1–5). Defaults to 1.
    /// </param>
    /// <param name="category">Optional category tag applied to all generated cases.</param>
    /// <param name="ct">Cancellation token.</param>
    public async IAsyncEnumerable<GoldenCase> GenerateAsync(
        IEnumerable<RetrievedChunk> chunks,
        int queriesPerChunk = 1,
        string? category = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        if (queriesPerChunk is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(queriesPerChunk), "Must be in [1, 5].");

        var caseIndex = 0;
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            var prompt = BuildPrompt(chunk, queriesPerChunk);
            string response;
            try
            {
                response = await _llm.CompleteAsync(prompt, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Emit nothing for failed chunks; caller can log separately.
                continue;
            }

            var queries = ParseQueries(response);
            foreach (var query in queries)
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                yield return new GoldenCase
                {
                    Id = $"gen-{++caseIndex:D4}",
                    Query = query.Trim(),
                    RelevantChunkIds = [chunk.Id],
                    Category = category,
                    Description = "[GENERATED — needs human review]",
                };
            }
        }
    }

    /// <summary>
    /// Serialises generated cases to a JSON file suitable for use as a golden set.
    /// Existing file is overwritten.
    /// </summary>
    public async Task WriteJsonAsync(IAsyncEnumerable<GoldenCase> cases, string path,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var list = new List<GoldenCase>();
        await foreach (var c in cases.WithCancellation(ct)) list.Add(c);

        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, list, new JsonSerializerOptions { WriteIndented = true }, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the query-generation prompt. Override to customise the instruction format.
    /// </summary>
    protected virtual string BuildPrompt(RetrievedChunk chunk, int count) =>
        $"""
        You are a retrieval evaluation expert. Generate {count} distinct, realistic search
        quer{(count == 1 ? "y" : "ies")} that a user might type to find the following chunk.
        Output one query per line. Do not number them, add bullet points, or add any explanation.

        Chunk content:
        {chunk.Content}
        """;

    private static IEnumerable<string> ParseQueries(string response) =>
        response
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.TrimStart('-', '*', '•', ' ', '\t'));
}
