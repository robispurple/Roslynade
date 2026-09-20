namespace Roslynade.agent
{
    public interface ICodeAnalysisAgent : IAsyncDisposable
    {
        Task InitializeAsync();

        IAsyncEnumerable<string> AnalyzeStreamAsync(
            string filePath,
            Action<string>? onStructureSummary = null,
            CancellationToken cancellationToken = default);

        Task AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);
    }
}

