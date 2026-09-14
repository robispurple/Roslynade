using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.AI.Foundry.Local;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace Roslynade.agent
{
    public class FoundryCodeAgent(string modelAlias = "qwen3.5-2b") : IAsyncDisposable
    {
        private readonly string _modelAlias = modelAlias;
        private IModel? _model;
        private OpenAIChatClient? _chatClient;

        public async Task InitializeAsync()
        {
            // 1. Initialize Foundry Local runtime singleton
            if (!FoundryLocalManager.IsInitialized)
            {
                await FoundryLocalManager.CreateAsync(
                    new Configuration { AppName = "CodeAgent" },
                    NullLogger.Instance);
            }

            var manager = FoundryLocalManager.Instance;
            var catalog = await manager.GetCatalogAsync();

            // 2. Fetch and download the model variant optimized for your hardware
            _model = await catalog.GetModelAsync(_modelAlias)
                ?? throw new InvalidOperationException($"Model '{_modelAlias}' not found in catalog.");

            await AnsiConsole.Status().StartAsync("Checking/Downloading model...", async ctx =>
            {
                await _model.DownloadAsync(progress =>
                {
                    ctx.Status($"Downloading {_modelAlias}: {progress:F1}%");
                });
            });

            // 3. Load model into memory/VRAM
            await _model.LoadAsync();
            _chatClient = await _model.GetChatClientAsync();
        }

        public async Task AnalyzeAsync(string filePath)
        {
            if (_chatClient == null)
                throw new InvalidOperationException("Agent must be initialized before analyzing.");

            string code = await File.ReadAllTextAsync(filePath);
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var root = await syntaxTree.GetRootAsync();
            string structureSummary = RoslynAnalyzer.Analyze(root);

            var messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "system",
                    Content = "You are an expert C# static analysis assistant. Review code for bugs, performance bottlenecks, modern C# idiom improvements, and null-safety issues."
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

                    Provide concise, actionable recommendations with code snippets where applicable.
                    """
                }
            };

            AnsiConsole.MarkupLine("[bold cyan]AI Analysis:[/]\n");

            // Stream output directly to console
            var streamingResponse = _chatClient.CompleteChatStreamingAsync(messages, CancellationToken.None);
            await foreach (var chunk in streamingResponse)
            {
                var content = chunk.Choices?[0]?.Delta?.Content ?? chunk.Choices?[0]?.Message?.Content;
                if (!string.IsNullOrEmpty(content))
                {
                    Console.Write(content);
                }
            }
            Console.WriteLine();
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