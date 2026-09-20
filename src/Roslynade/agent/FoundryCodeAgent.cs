using Microsoft.AI.Foundry.Local;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Roslynade.Models;
using Spectre.Console;

namespace Roslynade.agent
{
    public class FoundryCodeAgent(string modelAlias = "qwen3.5-2b", bool preferGpu = true) : BaseCodeAnalysisAgent
    {
        private readonly string _modelAlias = modelAlias;
        private readonly bool _preferGpu = preferGpu;
        private IModel? _model;
        private ChatSession? _chatSession;

        public override async Task InitializeAsync()
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

        protected override void EnsureInitialized()
        {
            if (_model == null)
                throw new InvalidOperationException("Agent must be initialized before analyzing.");
        }

        protected override async IAsyncEnumerable<string> GenerateStreamAsync(
            string systemPrompt,
            string userPrompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_model == null)
                throw new InvalidOperationException("Agent must be initialized before analyzing.");

            using var session = new ChatSession(_model);
            session.SetStreaming(true);

            using var request = new Request();
            request.SetOptions(new RequestOptions
            {
                Search = new SearchOptions
                {
                    MaxOutputTokens = MaxOutputTokens
                }
            });

            request.AddItem(new MessageItem(MessageRole.System, systemPrompt));
            request.AddItem(new MessageItem(MessageRole.User, userPrompt));

            await using var streamingResponse = session.ProcessStreamingRequestAsync(request, cancellationToken);
            await foreach (var item in streamingResponse.WithCancellation(cancellationToken))
            {
                if (item is TextItem textItem && !string.IsNullOrEmpty(textItem.Text))
                {
                    yield return textItem.Text;
                }
            }
        }

        public override async ValueTask DisposeAsync()
        {
            _chatSession?.Dispose();
            if (_model != null)
            {
                await _model.UnloadAsync();
            }
        }
    }
}