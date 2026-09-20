using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.AI;
using OpenAI;
using Roslynade.Models;
using Spectre.Console;

namespace Roslynade.agent
{
    public class ApiEndpointCodeAgent : BaseCodeAnalysisAgent
    {
        private readonly string _endpoint;
        private readonly string _modelName;
        private readonly string? _apiKey;
        private IChatClient? _chatClient;
        private readonly bool _ownsClient;

        public ApiEndpointCodeAgent(
            string endpoint,
            string modelName,
            string? apiKey = null,
            IChatClient? customChatClient = null)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint URL cannot be null or empty.", nameof(endpoint));
            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException("Model name cannot be null or empty.", nameof(modelName));

            _endpoint = endpoint.Trim().TrimEnd('/');
            _modelName = modelName.Trim();
            _apiKey = apiKey?.Trim();

            if (customChatClient != null)
            {
                _chatClient = customChatClient;
                _ownsClient = false;
            }
            else
            {
                _ownsClient = true;
            }
        }

        public string Endpoint => _endpoint;
        public string ModelName => _modelName;

        public override Task InitializeAsync()
        {
            if (_chatClient != null)
            {
                return Task.CompletedTask;
            }

            string resolvedKey = ResolveApiKey(_apiKey);

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(_endpoint)
            };

            var credential = new ApiKeyCredential(resolvedKey);
            var openAiClient = new OpenAIClient(credential, clientOptions);
            _chatClient = openAiClient.GetChatClient(_modelName).AsIChatClient();

            return Task.CompletedTask;
        }

        public static string ResolveApiKey(string? explicitApiKey)
        {
            if (!string.IsNullOrWhiteSpace(explicitApiKey))
            {
                return explicitApiKey.Trim();
            }

            // Fallback to common environment variables
            var envCandidate = Environment.GetEnvironmentVariable("ROSLYNADE_API_KEY")
                ?? Environment.GetEnvironmentVariable("AZURE_AI_KEY")
                ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN")
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            if (!string.IsNullOrWhiteSpace(envCandidate))
            {
                return envCandidate.Trim();
            }

            // For local inference servers (e.g. Ollama, LM Studio, vLLM) where authentication
            // is not enforced, default to a dummy token since SDKs require non-empty credentials.
            return "not-needed";
        }

        protected override string AgentDisplayName => "AI Analysis (Remote API)";

        protected override void EnsureInitialized()
        {
            if (_chatClient == null)
                throw new InvalidOperationException("Agent must be initialized before analyzing.");
        }

        protected override async IAsyncEnumerable<string> GenerateStreamAsync(
            string systemPrompt,
            string userPrompt,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_chatClient == null)
                throw new InvalidOperationException("Agent must be initialized before analyzing.");

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            var options = new ChatOptions
            {
                MaxOutputTokens = MaxOutputTokens
            };

            await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    yield return update.Text;
                }
            }
        }

        public override ValueTask DisposeAsync()
        {
            if (_ownsClient && _chatClient != null)
            {
                _chatClient.Dispose();
            }
            return ValueTask.CompletedTask;
        }
    }
}
