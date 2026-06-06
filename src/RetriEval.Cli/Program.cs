using System.CommandLine;
using RetriEval.Cli.Commands;

var root = new RootCommand("retrieval-eval — RetriEval CLI for evaluating RAG retrieval quality.");
root.AddCommand(InitCommand.Build());
root.AddCommand(RunCommand.Build());
root.AddCommand(CompareCommand.Build());
root.AddCommand(GenerateCommand.Build());

return await root.InvokeAsync(args);
