using Roslynade;
using Roslynade.agent;
using Roslynade.Ui;
using Spectre.Console;

var options = CommandLineOptions.Parse(args);

if (options.ResolvedFiles.Count == 0)
{
    AnsiConsole.MarkupLine("[red]Usage:[/] dotnet run -- <csharp-files-or-directories...> " +
                           "[[--model <model>]] [[--device <gpu|cpu>]] [[--gpu|--cpu]] " +
                           "[[--endpoint <api-url>]] [[--api-key <key>]] [[--concurrency|-c <N>]]");
    AnsiConsole.MarkupLine("[grey]Examples:[/] (Foundry Local)");
    AnsiConsole.MarkupLine("  dotnet run -- File1.cs File2.cs");
    AnsiConsole.MarkupLine("  dotnet run -- ./src --model qwen2.5-coder-14b --gpu");
    AnsiConsole.MarkupLine("  dotnet run -- ./src --device cpu -c 2");
    AnsiConsole.MarkupLine("[grey]Examples:[/] (Remote API Endpoint)");
    AnsiConsole.MarkupLine("  dotnet run -- ./src --endpoint https://models.inference.ai.azure.com --model gpt-4o");
    AnsiConsole.MarkupLine("  dotnet run -- ./src --endpoint http://localhost:11434/v1 --model qwen2.5-coder:14b -c 4");
    return;
}

ICodeAnalysisAgent agent;
int concurrency = options.ResolveConcurrency();

if (!string.IsNullOrWhiteSpace(options.ExplicitEndpoint))
{
    // Remote API Endpoint flow
    AnsiConsole.Write(new Rule("[yellow]Roslynade C# Multi-File Analyzer (API Endpoint)[/]").LeftJustified());
    AnsiConsole.MarkupLine($"Discovered [green]{options.ResolvedFiles.Count}[/] file(s) for analysis.");
    string concurrencySource = options.ExplicitConcurrency.HasValue ? "user-specified" : "API endpoint default";
    AnsiConsole.MarkupLine($"Analysis Concurrency: [bold cyan]{concurrency}[/] [grey]({concurrencySource})[/]\n");

    string selectedModel;
    if (!string.IsNullOrWhiteSpace(options.ExplicitModel))
    {
        selectedModel = options.ExplicitModel.Trim();
    }
    else
    {
        bool isInteractive = AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected;
        selectedModel = isInteractive
            ? AnsiConsole.Ask<string>("Enter model name for endpoint (e.g. [green]gpt-4o[/], [green]deepseek-r1[/]):", "gpt-4o").Trim()
            : "gpt-4o";
    }

    AnsiConsole.MarkupLine($"Using API Endpoint: [bold cyan]{Markup.Escape(options.ExplicitEndpoint)}[/]");
    AnsiConsole.MarkupLine($"Using Model:        [bold green]{Markup.Escape(selectedModel)}[/]\n");

    agent = new ApiEndpointCodeAgent(
        endpoint: options.ExplicitEndpoint,
        modelName: selectedModel,
        apiKey: options.ExplicitApiKey);
}
else
{
    // Foundry Local flow
    AnsiConsole.Write(new Rule("[yellow]Foundry Local C# Multi-File Analyzer[/]").LeftJustified());
    AnsiConsole.MarkupLine($"Discovered [green]{options.ResolvedFiles.Count}[/] file(s) for analysis.");
    string concurrencySource = options.ExplicitConcurrency.HasValue ? "user-specified" : "Foundry Local default";
    AnsiConsole.MarkupLine($"Analysis Concurrency: [bold cyan]{concurrency}[/] [grey]({concurrencySource})[/]\n");

    string selectedModel;
    if (!string.IsNullOrWhiteSpace(options.ExplicitModel))
    {
        selectedModel = options.ExplicitModel.Trim();
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

    string selectedDevice = ModelSelectorUi.SelectDevice(options.ExplicitDevice);
    bool preferGpu = selectedDevice.Equals("GPU", StringComparison.OrdinalIgnoreCase);
    AnsiConsole.MarkupLine($"Using compute device: [bold cyan]{selectedDevice}[/]\n");

    agent = new FoundryCodeAgent(modelAlias: selectedModel, preferGpu: preferGpu);
}

await using (agent)
{
    await agent.InitializeAsync();
    var session = new AnalysisSession(agent, options.ResolvedFiles, maxConcurrency: concurrency);
    await session.RunAsync();
}