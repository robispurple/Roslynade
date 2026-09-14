using Roslynade.agent;
using Spectre.Console;

if (args.Length == 0 || !File.Exists(args[0]))
{
    AnsiConsole.MarkupLine("[red]Usage:[/] dotnet run -- <path-to-csharp-file>");
    return;
}

string targetFile = Path.GetFullPath(args[0]);

AnsiConsole.Write(new Rule("[yellow]Foundry Local C# Analyzer[/]").LeftJustified());
AnsiConsole.MarkupLine($"Analyzing: [green]{targetFile}[/]\n");

await using var agent = new FoundryCodeAgent(modelAlias: "qwen3.5-2b");
await agent.InitializeAsync();
await agent.AnalyzeAsync(targetFile);