using Microsoft.AI.Foundry.Local;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Roslynade.Models;
using Spectre.Console;

namespace Roslynade.agent
{
    public class FoundryCodeAgent(string modelAlias = "qwen3.5-2b", bool preferGpu = true) : ICodeAnalysisAgent
    {
        private readonly string _modelAlias = modelAlias;
        private readonly bool _preferGpu = preferGpu;
        private IModel? _model;
        private ChatSession? _chatSession;

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
                        await manager.DownloadAndRegisterEpsAsync((epName, percent) =>
                        {
                            ctx.Status($"Registering hardware Execution Provider [bold cyan]{Markup.Escape(epName)}[/]: {percent:F1}%");
                        });
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
            _chatSession = new ChatSession(_model);
            _chatSession.SetStreaming(true);
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
            if (_model == null)
                throw new InvalidOperationException("Agent must be initialized before analyzing.");

            using var session = new ChatSession(_model);
            session.SetStreaming(true);

            string code = await File.ReadAllTextAsync(filePath, cancellationToken);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);
            string structureSummary = RoslynAnalyzer.Analyze(root);
            onStructureSummary?.Invoke(structureSummary);

            using var request = new Request();
            request.SetOptions(new RequestOptions
            {
                Search = new SearchOptions
                {
                    MaxOutputTokens = 8000
                }
            });

            request.AddItem(new MessageItem(MessageRole.System, CodeReviewPrompts.SystemPrompt));
            request.AddItem(new MessageItem(MessageRole.User, CodeReviewPrompts.BuildUserPrompt(structureSummary, Path.GetExtension(filePath), code)));

            var buffer = new System.Text.StringBuilder();
            await using var streamingResponse = session.ProcessStreamingRequestAsync(request, cancellationToken);
            await foreach (var item in streamingResponse.WithCancellation(cancellationToken))
            {
                if (item is TextItem textItem && !string.IsNullOrEmpty(textItem.Text))
                {
                    yield return textItem.Text;
                    buffer.Append(textItem.Text);

                    if ((textItem.Text.Contains('}') || textItem.Text.Contains('`') || buffer.Length > 200) &&
                        CodeReviewParser.TryParse(buffer.ToString(), out _))
                    {
                        yield break;
                    }
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _chatSession?.Dispose();
            if (_model != null)
            {
                await _model.UnloadAsync();
            }
        }
    }
}