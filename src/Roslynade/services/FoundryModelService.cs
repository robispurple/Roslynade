using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Roslynade.Models;

namespace Roslynade.Services
{
    public class FoundryCliException : Exception
    {
        public bool IsCliNotFound { get; }

        public FoundryCliException(string message, bool isCliNotFound = false, Exception? innerException = null)
            : base(message, innerException)
        {
            IsCliNotFound = isCliNotFound;
        }
    }

    public static class FoundryModelService
    {
        public static async Task<IReadOnlyList<FoundryModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "foundry",
                Arguments = "model list -o json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process;
            try
            {
                process = Process.Start(startInfo)
                    ?? throw new FoundryCliException("Failed to launch 'foundry' process.", isCliNotFound: false);
            }
            catch (Win32Exception winEx)
            {
                throw new FoundryCliException(
                    "The Foundry CLI ('foundry') was not found on your system PATH. " +
                    "Please ensure it is installed using 'winget install Microsoft.FoundryLocal'.",
                    isCliNotFound: true,
                    innerException: winEx);
            }
            catch (Exception ex)
            {
                throw new FoundryCliException($"Error executing 'foundry model list': {ex.Message}", innerException: ex);
            }

            using (process)
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

                await process.WaitForExitAsync(cancellationToken);

                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                if (process.ExitCode != 0)
                {
                    throw new FoundryCliException(
                        $"Command 'foundry model list -o json' exited with code {process.ExitCode}.\nError: {stderr.Trim()}");
                }

                if (string.IsNullOrWhiteSpace(stdout))
                {
                    return [];
                }

                try
                {
                    // Locate beginning of JSON object if there's any surrounding console banner
                    int jsonStart = stdout.IndexOf('{');
                    int jsonEnd = stdout.LastIndexOf('}');

                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        stdout = stdout.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    }

                    var catalog = JsonSerializer.Deserialize<FoundryModelCatalogResponse>(stdout, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return catalog?.Models ?? [];
                }
                catch (JsonException jEx)
                {
                    throw new FoundryCliException(
                        $"Failed to parse model list JSON output from 'foundry model list': {jEx.Message}\nOutput received:\n{stdout}",
                        innerException: jEx);
                }
            }
        }
    }
}

