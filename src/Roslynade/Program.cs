using Roslynade;
using Roslynade.agent;
using Roslynade.Ui;
using Spectre.Console;

string? explicitModel = null;
string? explicitDevice = null;
string? explicitEndpoint = null;
string? explicitApiKey = null;
var resolvedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

for (int i = 0; i < args.Length; i++)
{
    var arg = args[i];

    if (arg.Equals("--gpu", StringComparison.OrdinalIgnoreCase))
    {
        explicitDevice = "GPU";
        continue;
    }
    if (arg.Equals("--cpu", StringComparison.OrdinalIgnoreCase))
    {
        explicitDevice = "CPU";
        continue;
    }
    if (arg.Equals("--device", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        explicitDevice = args[++i];
        continue;
    }
    if (arg.StartsWith("--device=", StringComparison.OrdinalIgnoreCase))
    {
        explicitDevice = arg.Substring("--device=".Length);
        continue;
    }
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
    if ((arg.Equals("--endpoint", StringComparison.OrdinalIgnoreCase) ||
         arg.Equals("--api-endpoint", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
    {
        explicitEndpoint = args[++i];
        continue;
    }
    if (arg.StartsWith("--endpoint=", StringComparison.OrdinalIgnoreCase))
    {
        explicitEndpoint = arg.Substring("--endpoint=".Length);
        continue;
    }
    if (arg.StartsWith("--api-endpoint=", StringComparison.OrdinalIgnoreCase))
    {
        explicitEndpoint = arg.Substring("--api-endpoint=".Length);
        continue;
    }
    if (arg.Equals("--api-key", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        explicitApiKey = args[++i];
        continue;
    }
    if (arg.StartsWith("--api-key=", StringComparison.OrdinalIgnoreCase))
    {
        explicitApiKey = arg.Substring("--api-key=".Length);
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
    AnsiConsole.MarkupLine("[red]Usage:[/] dotnet run -- <csharp-files-or-directories...> " +
                           "[[--model <model>]] [[--device <gpu|cpu>]] [[--gpu|--cpu]] " +
                           "[[--endpoint <api-url>]] [[--api-key <key>]]");
    AnsiConsole.MarkupLine("[grey]Examples:[/] (Foundry Local)");
    AnsiConsole.MarkupLine("  dotnet run -- File1.cs File2.cs");
    AnsiConsole.MarkupLine("  dotnet run -- ./src --model qwen2.5-coder-14b --gpu");
    AnsiConsole.MarkupLine("  dotnet run -- ./src --device cpu");
    AnsiConsole.MarkupLine("[grey]Examples:[/] (Remote API Endpoint)");
    AnsiConsole.MarkupLine("  dotnet run -- ./src --endpoint https://models.inference.ai.azure.com --model gpt-4o");
    AnsiConsole.MarkupLine("  dotnet run -- ./src --endpoint http://localhost:11434/v1 --model qwen2.5-coder:14b");
    return;
}

ICodeAnalysisAgent agent;

if (!string.IsNullOrWhiteSpace(explicitEndpoint))
{
    // Remote API Endpoint flow
    AnsiConsole.Write(new Rule("[yellow]Roslynade C# Multi-File Analyzer (API Endpoint)[/]").LeftJustified());
    AnsiConsole.MarkupLine($"Discovered [green]{resolvedFiles.Count}[/] file(s) for analysis.\n");

    string selectedModel;
    if (!string.IsNullOrWhiteSpace(explicitModel))
    {
        selectedModel = explicitModel.Trim();
    }
    else
    {
        bool isInteractive = AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected;
        selectedModel = isInteractive
            ? AnsiConsole.Ask<string>("Enter model name for endpoint (e.g. [green]gpt-4o[/], [green]deepseek-r1[/]):", "gpt-4o").Trim()
            : "gpt-4o";
    }

    AnsiConsole.MarkupLine($"Using API Endpoint: [bold cyan]{Markup.Escape(explicitEndpoint)}[/]");
    AnsiConsole.MarkupLine($"Using Model:        [bold green]{Markup.Escape(selectedModel)}[/]\n");

    agent = new ApiEndpointCodeAgent(
        endpoint: explicitEndpoint,
        modelName: selectedModel,
        apiKey: explicitApiKey);
}
else
{
    // Foundry Local flow
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

    string selectedDevice = ModelSelectorUi.SelectDevice(explicitDevice);
    bool preferGpu = selectedDevice.Equals("GPU", StringComparison.OrdinalIgnoreCase);
    AnsiConsole.MarkupLine($"Using compute device: [bold cyan]{selectedDevice}[/]\n");

    agent = new FoundryCodeAgent(modelAlias: selectedModel, preferGpu: preferGpu);
}

await using (agent)
{
    await agent.InitializeAsync();
    var session = new AnalysisSession(agent, resolvedFiles, maxConcurrency: 1);
    await session.RunAsync();
}