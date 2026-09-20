namespace Roslynade
{
    public class CommandLineOptions
    {
        public string? ExplicitModel { get; set; }
        public string? ExplicitDevice { get; set; }
        public string? ExplicitEndpoint { get; set; }
        public string? ExplicitApiKey { get; set; }
        public int? ExplicitConcurrency { get; set; }
        public HashSet<string> ResolvedFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public int ResolveConcurrency()
        {
            if (ExplicitConcurrency.HasValue)
            {
                return Math.Max(1, ExplicitConcurrency.Value);
            }

            // Default to 3 for remote endpoints (batching / network parallelism)
            // Default to 1 for local inference (VRAM safety and memory-bandwidth bound generation)
            return !string.IsNullOrWhiteSpace(ExplicitEndpoint) ? 3 : 1;
        }

        public static CommandLineOptions Parse(string[] args)
        {
            var options = new CommandLineOptions();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.Equals("--gpu", StringComparison.OrdinalIgnoreCase))
                {
                    options.ExplicitDevice = "GPU";
                    continue;
                }
                if (arg.Equals("--cpu", StringComparison.OrdinalIgnoreCase))
                {
                    options.ExplicitDevice = "CPU";
                    continue;
                }
                if (arg.Equals("--device", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    options.ExplicitDevice = args[++i];
                    continue;
                }
                if (arg.StartsWith("--device=", StringComparison.OrdinalIgnoreCase))
                {
                    options.ExplicitDevice = arg.Substring("--device=".Length);
                    continue;
                }
                if (arg.Equals("--model", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    options.ExplicitModel = args[++i];
                    continue;
                }
                if (arg.StartsWith("--model=", StringComparison.OrdinalIgnoreCase))
                {
                    options.ExplicitModel = arg.Substring("--model=".Length);
                    continue;
                }
                if ((arg.Equals("--endpoint", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--api-endpoint", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    options.ExplicitEndpoint = args[++i];
                    continue;
                }
                if (arg.StartsWith("--endpoint=", StringComparison.OrdinalIgnoreCase))
                {
                    options.ExplicitEndpoint = arg.Substring("--endpoint=".Length);
                    continue;
                }
                if (arg.StartsWith("--api-endpoint=", StringComparison.OrdinalIgnoreCase))
                {
                    options.ExplicitEndpoint = arg.Substring("--api-endpoint=".Length);
                    continue;
                }
                if (arg.Equals("--api-key", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    options.ExplicitApiKey = args[++i];
                    continue;
                }
                if (arg.StartsWith("--api-key=", StringComparison.OrdinalIgnoreCase))
                {
                    options.ExplicitApiKey = arg.Substring("--api-key=".Length);
                    continue;
                }
                if ((arg.Equals("--concurrency", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-c", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[++i], out int cVal))
                    {
                        options.ExplicitConcurrency = cVal;
                    }
                    continue;
                }
                if (arg.StartsWith("--concurrency=", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(arg.Substring("--concurrency=".Length), out int cVal))
                    {
                        options.ExplicitConcurrency = cVal;
                    }
                    continue;
                }
                if (arg.StartsWith("-c=", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(arg.Substring("-c=".Length), out int cVal))
                    {
                        options.ExplicitConcurrency = cVal;
                    }
                    continue;
                }

                if (File.Exists(arg))
                {
                    options.ResolvedFiles.Add(Path.GetFullPath(arg));
                }
                else if (Directory.Exists(arg))
                {
                    var dirFiles = Directory.EnumerateFiles(arg, "*.cs", SearchOption.AllDirectories)
                        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"));
                    foreach (var file in dirFiles)
                    {
                        options.ResolvedFiles.Add(Path.GetFullPath(file));
                    }
                }
                else
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(arg) ?? ".";
                        string pattern = Path.GetFileName(arg);
                        if (string.IsNullOrEmpty(dir)) dir = ".";
                        if (Directory.Exists(dir))
                        {
                            var matched = Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly);
                            foreach (var file in matched)
                            {
                                options.ResolvedFiles.Add(Path.GetFullPath(file));
                            }
                        }
                    }
                    catch
                    {
                        // Ignore invalid pattern formatting
                    }
                }
            }

            return options;
        }
    }
}

