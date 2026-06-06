using System.CommandLine;
using System.Text.Json;

namespace RetriEval.Cli.Commands;

internal static class InitCommand
{
    internal static Command Build()
    {
        var outputOption = new Option<string>(
            "--output", () => ".", "Directory in which to scaffold the files.");

        var cmd = new Command("init",
            "Scaffold a starter golden-set JSON file and a retrieval-eval config in the target directory.");
        cmd.AddOption(outputOption);

        cmd.SetHandler(async (string output) =>
        {
            Directory.CreateDirectory(output);
            await WriteGoldenSetAsync(output);
            await WriteConfigAsync(output);
            Console.WriteLine($"Scaffolded files in: {Path.GetFullPath(output)}");
            Console.WriteLine("  golden-set.json  — add your hand-labeled cases here");
            Console.WriteLine("  retrieval-eval.json  — configure your retriever and thresholds");
        }, outputOption);

        return cmd;
    }

    private static async Task WriteGoldenSetAsync(string dir)
    {
        var path = Path.Combine(dir, "golden-set.json");
        if (File.Exists(path)) { Console.WriteLine($"  Skipped (already exists): {path}"); return; }

        var sample = new[]
        {
            new
            {
                id = "q-001",
                query = "What is the capital of France?",
                relevantChunkIds = new[] { "chunk-paris-001" },
                relevantKeywords = new[] { "Paris", "capital", "France" },
                category = "geography",
                description = "Example: basic factual retrieval",
            },
        };
        await File.WriteAllTextAsync(path,
            JsonSerializer.Serialize(sample, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"  Created: {path}");
    }

    private static async Task WriteConfigAsync(string dir)
    {
        var path = Path.Combine(dir, "retrieval-eval.json");
        if (File.Exists(path)) { Console.WriteLine($"  Skipped (already exists): {path}"); return; }

        var config = new
        {
            goldenSetPath = "golden-set.json",
            reportPath = "retrieval-report",
            k = 5,
            maxConcurrency = 4,
            grader = "ChunkIdGrader",
            thresholds = new
            {
                hitAtK = 0.80,
                mrr = 0.60,
                ndcgAtK = 0.60,
            },
        };
        await File.WriteAllTextAsync(path,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"  Created: {path}");
    }
}
