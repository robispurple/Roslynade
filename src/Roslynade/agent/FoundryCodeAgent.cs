using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.AI.Foundry.Local;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace Roslynade.agent
{
    public class FoundryCodeAgent(string modelAlias = "qwen3.5-2b", bool preferGpu = true) : IAsyncDisposable
    {
        private readonly string _modelAlias = modelAlias;
        private readonly bool _preferGpu = preferGpu;
        private IModel? _model;
        private OpenAIChatClient? _chatClient;

        public async Task InitializeAsync()
        {
            // 1. Initialize Foundry Local runtime singleton pointing to global .foundry cache
            if (!FoundryLocalManager.IsInitialized)
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string globalFoundryDir = Path.Combine(userProfile, ".foundry");
                string globalModelCache = Path.Combine(globalFoundryDir, "cache", "models");

                await FoundryLocalManager.CreateAsync(
                    new Configuration
                    {
                        AppName = "foundry",
                        AppDataDir = globalFoundryDir,
                        ModelCacheDir = globalModelCache
                    },
                    NullLogger.Instance);
            }

            var manager = FoundryLocalManager.Instance;

            // 2. Download and register hardware Execution Providers (e.g. WebGPU for AMD/generic GPUs)
            if (_preferGpu)
            {
                try
                {
                    await AnsiConsole.Status().StartAsync("Registering hardware Execution Providers...", async ctx =>
                    {
                        await manager.DownloadAndRegisterEpsAsync();
                    });
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]Notice:[/] Could not download/register additional execution providers: [grey]{Markup.Escape(ex.Message)}[/]");
                }
            }

            var catalog = await manager.GetCatalogAsync();

            // 3. Fetch and resolve the model variant (preferring GPU when available)
            _model = await catalog.GetModelAsync(_modelAlias)
                ?? throw new InvalidOperationException($"Model '{_modelAlias}' not found in catalog.");

            if (_preferGpu)
            {
                if (_model.Info?.Runtime?.DeviceType != DeviceType.GPU)
                {
                    var gpuVariant = _model.Variants?.FirstOrDefault(v =>
                        v.Info?.Runtime?.DeviceType == DeviceType.GPU ||
                        v.Id.Contains("gpu", StringComparison.OrdinalIgnoreCase));

                    if (gpuVariant != null)
                    {
                        _model = gpuVariant;
                    }
                }
            }
            else
            {
                if (_model.Info?.Runtime?.DeviceType != DeviceType.CPU)
                {
                    var cpuVariant = _model.Variants?.FirstOrDefault(v =>
                        v.Info?.Runtime?.DeviceType == DeviceType.CPU ||
                        v.Id.Contains("cpu", StringComparison.OrdinalIgnoreCase));

                    if (cpuVariant != null)
                    {
                        _model = cpuVariant;
                    }
                }
            }

            string targetDevice = _model.Info?.Runtime?.DeviceType.ToString() ?? "Unknown";
            string targetEp = _model.Info?.Runtime?.ExecutionProvider ?? "Default";
            AnsiConsole.MarkupLine($"[grey]Hardware Target:[/] [bold cyan]{targetDevice}[/] [grey]({targetEp})[/] [grey]for[/] [bold white]{Markup.Escape(_model.Id)}[/]\n");

            await AnsiConsole.Status().StartAsync($"Checking/Downloading {_model.Alias}...", async ctx =>
            {
                await _model.DownloadAsync(progress =>
                {
                    ctx.Status($"Downloading {_model.Alias} ({targetDevice}): {progress:F1}%");
                });
            });

            // 4. Load model into memory/VRAM
            await _model.LoadAsync();
            _chatClient = await _model.GetChatClientAsync();
        }

        public async Task AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            AnsiConsole.MarkupLine("[bold cyan]AI Analysis:[/]\n");
            await foreach (var chunk in AnalyzeStreamAsync(filePath, null, cancellationToken))
            {
                Console.Write(chunk);
            }
            Console.WriteLine();
        }

        public async IAsyncEnumerable<string> AnalyzeStreamAsync(
            string filePath,
            Action<string>? onStructureSummary = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_chatClient == null)
                throw new InvalidOperationException("Agent must be initialized before analyzing.");

            string code = await File.ReadAllTextAsync(filePath, cancellationToken);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);
            string structureSummary = RoslynAnalyzer.Analyze(root);
            onStructureSummary?.Invoke(structureSummary);

            var messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "system",
                    Content = """
                    You are an expert C# static analysis engine.
                    Review the given C# code for bugs, performance bottlenecks, modern C# idiom improvements, and null-safety issues.
                    Respond ONLY with a valid JSON object matching this schema:
                    {
                      "summary": "Brief executive summary of code quality and key findings.",
                      "overallScore": 85,
                      "issues": [
                        {
                          "severity": "Error" | "Warning" | "Suggestion",
                          "line": 12,
                          "title": "Short title describing the issue",
                          "description": "Clear explanation of why this is a problem and what should be done.",
                          "suggestedFix": "Code snippet illustrating the fix (optional)"
                        }
                      ]
                    }
                    Rules:
                    - overallScore must be an integer between 0 and 100.
                    - severity must be one of: "Error", "Warning", "Suggestion".
                    - Output raw valid JSON only. Do not include markdown code block formatting or explanations outside the JSON.
                    """
                },
                new()
                {
                    Role = "user",
                    Content = $"""
                    File summary: {structureSummary}

                    Code under review:
                    ```{Path.GetExtension(filePath).TrimStart('.')}
                    {code}
                    ```
                    """
                }
            };

            var streamingResponse = _chatClient.CompleteChatStreamingAsync(messages, cancellationToken);
            await foreach (var chunk in streamingResponse.WithCancellation(cancellationToken))
            {
                var content = chunk.Choices?[0]?.Delta?.Content ?? chunk.Choices?[0]?.Message?.Content;
                if (!string.IsNullOrEmpty(content))
                {
                    yield return content;
                }

                var finishReason = chunk.Choices?[0]?.FinishReason;
                if (!string.IsNullOrEmpty(finishReason))
                {
                    break;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_model != null)
            {
                await _model.UnloadAsync();
            }
        }
    }
}