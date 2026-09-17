using Roslynade;
using Roslynade.agent;
using Roslynade.Ui;
using Spectre.Console;

string? explicitModel = null;
var resolvedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

for (int i = 0; i < args.Length; i++)
{
    var arg = args[i];

    if (arg.Equals("--model", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        explicitModel = args[++i];
        continue;
    }
    if (arg.StartsWith("--model=", StringComparison.OrdinalIgnoreCase))
    {
        explicitModel = arg.Substring("--model=".Length);
        continue;
    }

    if (File.Exists(arg))
    {
        resolvedFiles.Add(Path.GetFullPath(arg));
    }
    else if (Directory.Exists(arg))
    {
        var dirFiles = Directory.EnumerateFiles(arg, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"));
        foreach (var file in dirFiles)
        {
            resolvedFiles.Add(Path.GetFullPath(file));
        }
    }
    else
    {
        try
        {
            string dir = Path.GetDirectoryName(arg) ?? ".";
            string pattern = Path.GetFileName(arg);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            if (Directory.Exists(dir))
            {
                var matched = Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly);
                foreach (var file in matched)
                {
                    resolvedFiles.Add(Path.GetFullPath(file));
                }
            }
        }
        catch
        {
            // Ignore invalid pattern formatting
        }
    }
}

if (resolvedFiles.Count == 0)
{
    AnsiConsole.MarkupLine("[red]Usage:[/] dotnet run -- <csharp-files-or-directories...> [[--model <model-alias>]]");
    AnsiConsole.MarkupLine("[grey]Examples:[/]");
    AnsiConsole.MarkupLine("  dotnet run -- File1.cs File2.cs");
    AnsiConsole.MarkupLine("  dotnet run -- ./src");
    AnsiConsole.MarkupLine("  dotnet run -- ./src --model qwen2.5-coder-14b");
    return;
}

AnsiConsole.Write(new Rule("[yellow]Foundry Local C# Multi-File Analyzer[/]").LeftJustified());
AnsiConsole.MarkupLine($"Discovered [green]{resolvedFiles.Count}[/] file(s) for analysis.\n");

string selectedModel;
if (!string.IsNullOrWhiteSpace(explicitModel))
{
    selectedModel = explicitModel.Trim();
    AnsiConsole.MarkupLine($"Using specified model: [bold green]{Markup.Escape(selectedModel)}[/]\n");
}
else
{
    try
    {
        selectedModel = await ModelSelectorUi.SelectModelAsync();
    }
    catch (OperationCanceledException)
    {
        AnsiConsole.MarkupLine("[yellow]Model selection cancelled. Exiting.[/]");
        return;
    }
}

await using var agent = new FoundryCodeAgent(modelAlias: selectedModel);
await agent.InitializeAsync();

var session = new AnalysisSession(agent, resolvedFiles, maxConcurrency: 1);
await session.RunAsync();