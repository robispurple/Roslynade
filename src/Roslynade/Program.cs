using Roslynade;
using Roslynade.agent;
using Spectre.Console;

var resolvedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var arg in args)
{
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
    AnsiConsole.MarkupLine("[red]Usage:[/] dotnet run -- <csharp-files-or-directories...>");
    AnsiConsole.MarkupLine("[grey]Examples:[/]");
    AnsiConsole.MarkupLine("  dotnet run -- File1.cs File2.cs");
    AnsiConsole.MarkupLine("  dotnet run -- ./src");
    return;
}

AnsiConsole.Write(new Rule("[yellow]Foundry Local C# Multi-File Analyzer[/]").LeftJustified());
AnsiConsole.MarkupLine($"Discovered [green]{resolvedFiles.Count}[/] file(s) for analysis.\n");

await using var agent = new FoundryCodeAgent(modelAlias: "qwen2.5-coder-14b");
await agent.InitializeAsync();

var session = new AnalysisSession(agent, resolvedFiles, maxConcurrency: 1);
await session.RunAsync();