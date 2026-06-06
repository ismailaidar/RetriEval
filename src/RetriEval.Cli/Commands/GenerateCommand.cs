using System.CommandLine;
using System.Text.Json;
using RetriEval.Core;
using RetriEval.Llm.Abstractions;

namespace RetriEval.Cli.Commands;

internal static class GenerateCommand
{
    internal static Command Build()
    {
        var corpusOption = new Option<string>(
            "--corpus", "Path to a JSON file containing an array of RetrievedChunk objects.")
        { IsRequired = true };
        var outputOption = new Option<string>(
            "--output", () => "generated-golden-set.json",
            "Output path for the generated golden-set JSON.");
        var queriesPerChunkOption = new Option<int>(
            "--queries-per-chunk", () => 1,
            "Number of queries to generate per chunk (1–5).");
        var categoryOption = new Option<string?>(
            "--category", () => null, "Optional category tag to apply to all generated cases.");

        var cmd = new Command("generate",
            "Synthesise a starter golden set from a corpus using an LLM. " +
            "⚠ Output requires human review before use in CI. " +
            "Requires RETRIEVAL_EVAL_LLM_ENDPOINT and RETRIEVAL_EVAL_LLM_KEY environment variables " +
            "pointing at an OpenAI-compatible chat completions endpoint.");
        cmd.AddOption(corpusOption);
        cmd.AddOption(outputOption);
        cmd.AddOption(queriesPerChunkOption);
        cmd.AddOption(categoryOption);

        cmd.SetHandler(async (string corpusPath, string output, int queriesPerChunk, string? category) =>
        {
            var endpoint = Environment.GetEnvironmentVariable("RETRIEVAL_EVAL_LLM_ENDPOINT");
            var apiKey   = Environment.GetEnvironmentVariable("RETRIEVAL_EVAL_LLM_KEY");

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
            {
                Console.Error.WriteLine(
                    "Set RETRIEVAL_EVAL_LLM_ENDPOINT and RETRIEVAL_EVAL_LLM_KEY to use 'generate'.");
                Environment.Exit(1);
                return;
            }

            if (!File.Exists(corpusPath))
            {
                Console.Error.WriteLine($"Corpus file not found: {corpusPath}");
                Environment.Exit(1);
                return;
            }

            List<RetrievedChunk>? corpus;
            try
            {
                var json = await File.ReadAllTextAsync(corpusPath);
                corpus = JsonSerializer.Deserialize<List<RetrievedChunk>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to parse corpus: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            if (corpus is null or { Count: 0 })
            {
                Console.Error.WriteLine("Corpus is empty.");
                Environment.Exit(1);
                return;
            }

            var llm = new HttpLlmClient(endpoint, apiKey);
            var generator = new GoldenSetGenerator(llm);

            Console.WriteLine($"Generating {queriesPerChunk} quer{(queriesPerChunk == 1 ? "y" : "ies")} " +
                              $"per chunk for {corpus.Count} chunks...");
            Console.WriteLine("⚠ Output requires human review before use as ground truth.");

            var cases = generator.GenerateAsync(corpus, queriesPerChunk, category);
            await generator.WriteJsonAsync(cases, output);

            Console.WriteLine($"Generated golden set written to: {Path.GetFullPath(output)}");

        }, corpusOption, outputOption, queriesPerChunkOption, categoryOption);

        return cmd;
    }
}

// ---------------------------------------------------------------------------
// Minimal OpenAI-compatible HTTP client — no SDK required
// ---------------------------------------------------------------------------

file sealed class HttpLlmClient(string endpoint, string apiKey) : ILlmClient
{
    private static readonly HttpClient Http = new();

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = "gpt-4o-mini",
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 256,
            temperature = 0.3,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
