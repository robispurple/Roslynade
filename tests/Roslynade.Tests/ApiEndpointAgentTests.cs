using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Roslynade.agent;
using Roslynade.Models;

namespace Roslynade.Tests
{
    public class ApiEndpointAgentTests
    {
        private class MockChatClient : IChatClient
        {
            private readonly IEnumerable<string> _chunks;
            public List<ChatMessage> ReceivedMessages { get; } = new();

            public MockChatClient(IEnumerable<string> chunks)
            {
                _chunks = chunks;
            }

            public ChatClientMetadata Metadata => new("MockProvider");

            public Task<ChatResponse> GetResponseAsync(
                IEnumerable<ChatMessage> chatMessages,
                ChatOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                ReceivedMessages.AddRange(chatMessages);
                var message = new ChatMessage(ChatRole.Assistant, string.Concat(_chunks));
                return Task.FromResult(new ChatResponse(message));
            }

            public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
                IEnumerable<ChatMessage> chatMessages,
                ChatOptions? options = null,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                ReceivedMessages.AddRange(chatMessages);
                foreach (var chunk in _chunks)
                {
                    yield return new ChatResponseUpdate
                    {
                        Contents = [new TextContent(chunk)]
                    };
                }
            }

            public object? GetService(Type serviceType, object? serviceKey = null) => null;

            public void Dispose() { }
        }

        [Fact]
        public void Constructor_NullOrWhitespaceEndpoint_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ApiEndpointCodeAgent("", "gpt-4o"));
            Assert.Throws<ArgumentException>(() => new ApiEndpointCodeAgent("   ", "gpt-4o"));
        }

        [Fact]
        public void Constructor_NullOrWhitespaceModel_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ApiEndpointCodeAgent("https://api.openai.com/v1", ""));
            Assert.Throws<ArgumentException>(() => new ApiEndpointCodeAgent("https://api.openai.com/v1", "   "));
        }

        [Fact]
        public void Constructor_TrimsTrailingSlashAndWhitespace()
        {
            var agent = new ApiEndpointCodeAgent("https://api.openai.com/v1/  ", "  gpt-4o  ");
            Assert.Equal("https://api.openai.com/v1", agent.Endpoint);
            Assert.Equal("gpt-4o", agent.ModelName);
        }

        [Fact]
        public void ResolveApiKey_ExplicitKeyTakesPrecedence()
        {
            string key = ApiEndpointCodeAgent.ResolveApiKey("custom-api-key-123");
            Assert.Equal("custom-api-key-123", key);
        }

        [Fact]
        public void ResolveApiKey_EnvVariableFallback()
        {
            string original = Environment.GetEnvironmentVariable("ROSLYNADE_API_KEY") ?? "";
            try
            {
                Environment.SetEnvironmentVariable("ROSLYNADE_API_KEY", "env-key-999");
                string key = ApiEndpointCodeAgent.ResolveApiKey(null);
                Assert.Equal("env-key-999", key);
            }
            finally
            {
                Environment.SetEnvironmentVariable("ROSLYNADE_API_KEY", string.IsNullOrEmpty(original) ? null : original);
            }
        }

        [Fact]
        public void ResolveApiKey_DefaultsToNotNeededWhenUnset()
        {
            // Clear env variables temporarily
            string? r = Environment.GetEnvironmentVariable("ROSLYNADE_API_KEY");
            string? a = Environment.GetEnvironmentVariable("AZURE_AI_KEY");
            string? g = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            string? o = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            try
            {
                Environment.SetEnvironmentVariable("ROSLYNADE_API_KEY", null);
                Environment.SetEnvironmentVariable("AZURE_AI_KEY", null);
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
                Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

                string key = ApiEndpointCodeAgent.ResolveApiKey(null);
                Assert.Equal("not-needed", key);
            }
            finally
            {
                Environment.SetEnvironmentVariable("ROSLYNADE_API_KEY", r);
                Environment.SetEnvironmentVariable("AZURE_AI_KEY", a);
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", g);
                Environment.SetEnvironmentVariable("OPENAI_API_KEY", o);
            }
        }

        [Fact]
        public async Task AnalyzeStreamAsync_YieldsTokensAndInvokesSummaryCallback()
        {
            var mockClient = new MockChatClient(new[] { "{\n  \"sum", "mary\": \"Looks good.\",", "\n  \"overallScore\": 95,\n  \"issues\": []\n}" });
            var agent = new ApiEndpointCodeAgent("https://api.openai.com/v1", "gpt-4o", customChatClient: mockClient);
            await agent.InitializeAsync();

            string tempFile = Path.GetTempFileName() + ".cs";
            await File.WriteAllTextAsync(tempFile, "public class Sample { public int Add(int a, int b) => a + b; }");

            try
            {
                string? capturedSummary = null;
                var chunks = new List<string>();

                await foreach (var chunk in agent.AnalyzeStreamAsync(tempFile, s => capturedSummary = s))
                {
                    chunks.Add(chunk);
                }

                Assert.NotEmpty(chunks);
                Assert.NotNull(capturedSummary);
                Assert.Contains("1 class(es)", capturedSummary);
                Assert.NotEmpty(mockClient.ReceivedMessages);
                Assert.Equal(2, mockClient.ReceivedMessages.Count);
                Assert.Equal(ChatRole.System, mockClient.ReceivedMessages[0].Role);
                Assert.Equal(ChatRole.User, mockClient.ReceivedMessages[1].Role);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void AnalysisSession_AcceptsApiEndpointCodeAgent()
        {
            var mockClient = new MockChatClient(new[] { "{}" });
            var agent = new ApiEndpointCodeAgent("https://api.openai.com/v1", "gpt-4o", customChatClient: mockClient);
            var session = new AnalysisSession(agent, new[] { "FakeFile.cs" }, maxConcurrency: 1);
            Assert.NotNull(session);
        }

        [Fact]
        public void ApiEndpointCodeAgent_InheritsBaseCodeAnalysisAgent_AndHasDefaultMaxTokens()
        {
            var agent = new ApiEndpointCodeAgent("https://api.openai.com/v1", "gpt-4o");
            Assert.IsAssignableFrom<BaseCodeAnalysisAgent>(agent);
            Assert.Equal(8000, agent.MaxOutputTokens);

            agent.MaxOutputTokens = 4096;
            Assert.Equal(4096, agent.MaxOutputTokens);
        }
    }
}
