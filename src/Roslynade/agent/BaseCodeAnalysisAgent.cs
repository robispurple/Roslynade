using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Roslynade.Models;
using Spectre.Console;

namespace Roslynade.agent
{
    public abstract class BaseCodeAnalysisAgent : ICodeAnalysisAgent
    {
        public int MaxOutputTokens { get; set; } = 8000;

        protected virtual string AgentDisplayName => "AI Analysis";

        public abstract Task InitializeAsync();

        public virtual async Task AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            AnsiConsole.MarkupLine($"[bold cyan]{AgentDisplayName}:[/]\n");
            await foreach (var chunk in AnalyzeStreamAsync(filePath, null, cancellationToken))
            {
                Console.Write(chunk);
            }
            Console.WriteLine();
        }

        public virtual async IAsyncEnumerable<string> AnalyzeStreamAsync(
            string filePath,
            Action<string>? onStructureSummary = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            string code = await File.ReadAllTextAsync(filePath, cancellationToken);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);
            string structureSummary = RoslynAnalyzer.Analyze(root);
            onStructureSummary?.Invoke(structureSummary);

            string systemPrompt = CodeReviewPrompts.SystemPrompt;
            string userPrompt = CodeReviewPrompts.BuildUserPrompt(structureSummary, Path.GetExtension(filePath), code);

            var buffer = new StringBuilder();
            await foreach (var token in GenerateStreamAsync(systemPrompt, userPrompt, cancellationToken).WithCancellation(cancellationToken))
            {
                if (!string.IsNullOrEmpty(token))
                {
                    yield return token;
                    buffer.Append(token);

                    if ((token.Contains('}') || token.Contains('`') || buffer.Length > 200) &&
                        CodeReviewParser.TryParse(buffer.ToString(), out _))
                    {
                        yield break;
                    }
                }
            }
        }

        protected abstract void EnsureInitialized();

        protected abstract IAsyncEnumerable<string> GenerateStreamAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken);

        public virtual ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}

