using Roslynade.Models;
using Roslynade.Services;
using Spectre.Console;

namespace Roslynade.Ui
{
    public static class ModelSelectorUi
    {
        private class ModelChoiceItem
        {
            public FoundryModelInfo? Model { get; init; }
            public string? CustomAction { get; init; }
            public required string DisplayMarkup { get; init; }
            public required string SearchText { get; init; }

            public override string ToString() => DisplayMarkup;
        }

        public static async Task<string> SelectModelAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                IReadOnlyList<FoundryModelInfo> models = [];
                bool fetchSuccess = false;
                Exception? failureEx = null;

                try
                {
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .StartAsync("Polling available Foundry Local models via 'foundry model list'...", async ctx =>
                        {
                            models = await FoundryModelService.GetAvailableModelsAsync(cancellationToken);
                            fetchSuccess = true;
                        });
                }
                catch (Exception ex)
                {
                    fetchSuccess = false;
                    failureEx = ex;
                }

                if (!fetchSuccess || models.Count == 0)
                {
                    var action = HandlePollingFailure(failureEx);
                    if (action.ActionType == FallbackActionType.Retry)
                    {
                        continue;
                    }
                    else if (action.ActionType == FallbackActionType.CustomAlias)
                    {
                        return action.CustomAlias!;
                    }
                    else
                    {
                        throw new OperationCanceledException("Model selection aborted by user.");
                    }
                }

                // Split into downloaded and undownloaded
                var downloaded = models.Where(m => m.Cached).OrderBy(m => m.Alias).ToList();
                var undownloaded = models.Where(m => !m.Cached).OrderBy(m => m.Alias).ToList();

                AnsiConsole.MarkupLine($"[grey]Discovered [bold white]{models.Count}[/] model(s) from Foundry Local: [/]" +
                                       $"[green]{downloaded.Count} downloaded[/], [yellow]{undownloaded.Count} available to download[/].\n");

                // If running in a non-interactive environment (e.g. redirection or CI), select the best default automatically
                bool isInteractive = AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected;
                if (!isInteractive)
                {
                    var defaultModel = downloaded.FirstOrDefault()?.Alias
                        ?? models.FirstOrDefault(m => m.Alias.Contains("coder", StringComparison.OrdinalIgnoreCase))?.Alias
                        ?? models.FirstOrDefault()?.Alias
                        ?? "qwen2.5-coder-14b";

                    AnsiConsole.MarkupLine($"[grey]Non-interactive environment detected; automatically defaulting to:[/] [bold green]{Markup.Escape(defaultModel)}[/]\n");
                    return defaultModel;
                }

                var prompt = new SelectionPrompt<ModelChoiceItem>()
                    .Title("[bold yellow]Select a Foundry Local model to use for analysis:[/]")
                    .PageSize(16)
                    .MoreChoicesText("[grey](Move up/down to browse, start typing to search)[/]")
                    .EnableSearch()
                    .UseConverter(item => item.DisplayMarkup);

                // Add downloaded models first
                foreach (var model in downloaded)
                {
                    prompt.AddChoice(new ModelChoiceItem
                    {
                        Model = model,
                        SearchText = $"{model.Alias} {model.Type} {model.Device} downloaded",
                        DisplayMarkup = $"[green]● [[Downloaded]][/]      [bold white]{Markup.Escape(model.Alias)}[/] [grey]({model.Device}, {model.FormattedSize}, {model.Type})[/]"
                    });
                }

                // Add undownloaded models
                foreach (var model in undownloaded)
                {
                    prompt.AddChoice(new ModelChoiceItem
                    {
                        Model = model,
                        SearchText = $"{model.Alias} {model.Type} {model.Device}",
                        DisplayMarkup = $"[yellow]○ [[Not Downloaded]][/]  [white]{Markup.Escape(model.Alias)}[/] [grey]({model.Device}, {model.FormattedSize}, {model.Type})[/]"
                    });
                }

                // Add utility options
                prompt.AddChoice(new ModelChoiceItem
                {
                    CustomAction = "refresh",
                    SearchText = "refresh poll models",
                    DisplayMarkup = "[cyan]↻ [[Refresh / Re-poll models]][/]"
                });

                prompt.AddChoice(new ModelChoiceItem
                {
                    CustomAction = "custom",
                    SearchText = "custom manual alias",
                    DisplayMarkup = "[grey]✍ [[Enter custom model alias manually]][/]"
                });

                var selected = AnsiConsole.Prompt(prompt);

                if (selected.CustomAction == "refresh")
                {
                    AnsiConsole.WriteLine();
                    continue;
                }

                if (selected.CustomAction == "custom")
                {
                    var customAlias = AnsiConsole.Ask<string>("Enter the Foundry model alias to use (e.g. [green]qwen2.5-coder-14b[/]):");
                    AnsiConsole.MarkupLine($"Using custom model alias: [bold green]{Markup.Escape(customAlias)}[/]\n");
                    return customAlias.Trim();
                }

                if (selected.Model != null)
                {
                    string cacheStatus = selected.Model.Cached
                        ? "[green]Downloaded[/]"
                        : "[yellow]Will download before running[/]";

                    AnsiConsole.MarkupLine($"Selected model: [bold green]{Markup.Escape(selected.Model.Alias)}[/] ({cacheStatus})\n");
                    return selected.Model.Alias;
                }
            }
        }

        private enum FallbackActionType
        {
            Retry,
            CustomAlias,
            Exit
        }

        private record FallbackResult(FallbackActionType ActionType, string? CustomAlias = null);

        private static FallbackResult HandlePollingFailure(Exception? ex)
        {
            string detail = ex?.Message ?? "No models returned from catalog.";

            var panel = new Panel(new Markup(
                "[bold red]Unable to dynamically query Foundry Local models via 'foundry model list'.[/]\n\n" +
                $"[grey]Reason:[/] {Markup.Escape(detail)}\n\n" +
                "[yellow]Tip:[/] The Foundry Local CLI can be installed with:\n" +
                "  [bold cyan]winget install Microsoft.FoundryLocal[/]\n\n" +
                "[grey]Make sure the CLI is installed and accessible on your PATH.[/]"
            ))
            {
                Header = new PanelHeader("[bold red]Foundry CLI Notice[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("How would you like to proceed?")
                    .AddChoices(
                        "Enter model alias manually (e.g. qwen2.5-coder-14b)",
                        "Retry polling",
                        "Exit"
                    ));

            if (choice.StartsWith("Enter model alias", StringComparison.OrdinalIgnoreCase))
            {
                var custom = AnsiConsole.Ask("Enter model alias:", "qwen2.5-coder-14b");
                return new FallbackResult(FallbackActionType.CustomAlias, custom.Trim());
            }

            if (choice.StartsWith("Retry", StringComparison.OrdinalIgnoreCase))
            {
                return new FallbackResult(FallbackActionType.Retry);
            }

            return new FallbackResult(FallbackActionType.Exit);
        }

        public static string SelectDevice(string? explicitDevice = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitDevice))
            {
                return explicitDevice.Equals("cpu", StringComparison.OrdinalIgnoreCase) ? "CPU" : "GPU";
            }

            bool isInteractive = AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected;
            if (!isInteractive)
            {
                return "GPU";
            }

            var prompt = new SelectionPrompt<string>()
                .Title("[bold yellow]Select compute hardware target:[/]")
                .AddChoices(
                    "GPU (Hardware Accelerated - Recommended)",
                    "CPU (System Processor)"
                );

            var choice = AnsiConsole.Prompt(prompt);
            return choice.StartsWith("GPU", StringComparison.OrdinalIgnoreCase) ? "GPU" : "CPU";
        }
    }
}
