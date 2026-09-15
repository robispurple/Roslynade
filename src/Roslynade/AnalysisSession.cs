using System.Collections.Concurrent;
using System.Text;
using Roslynade.agent;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Roslynade
{
    public enum AnalysisFileStatus
    {
        Pending,
        Analyzing,
        Done,
        Error
    }

    public class FileAnalysisState
    {
        public required string FilePath { get; init; }
        public required string FileName { get; init; }
        public AnalysisFileStatus Status { get; set; } = AnalysisFileStatus.Pending;
        public string? StructureSummary { get; set; }
        public StringBuilder OutputBuffer { get; } = new();
        public string? ErrorMessage { get; set; }
        public int ScrollOffset { get; set; }
        public bool AutoScroll { get; set; } = true;
        public object LockObj { get; } = new();
    }

    public class AnalysisSession
    {
        private readonly FoundryCodeAgent _agent;
        private readonly List<FileAnalysisState> _files;
        private readonly int _maxConcurrency;
        private int _activeTabIndex;

        public AnalysisSession(FoundryCodeAgent agent, IEnumerable<string> filePaths, int maxConcurrency = 2)
        {
            _agent = agent;
            _maxConcurrency = Math.Max(1, maxConcurrency);
            _files = filePaths.Select(path => new FileAnalysisState
            {
                FilePath = path,
                FileName = Path.GetFileName(path)
            }).ToList();
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            if (_files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No files to analyze.[/]");
                return;
            }

            // Check if interactive console features are supported
            bool isInteractive = !Console.IsInputRedirected &&
                                 !Console.IsOutputRedirected &&
                                 Environment.UserInteractive;

            if (isInteractive)
            {
                await RunInteractiveAsync(cancellationToken);
            }
            else
            {
                await RunSequentialFallbackAsync(cancellationToken);
            }

            RenderSummaryTable();
        }

        private async Task RunInteractiveAsync(CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var throttler = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

            // Start background analysis tasks
            var workerTasks = _files.Select(async file =>
            {
                await throttler.WaitAsync(cts.Token);
                try
                {
                    file.Status = AnalysisFileStatus.Analyzing;
                    await foreach (var chunk in _agent.AnalyzeStreamAsync(
                        file.FilePath,
                        summary => file.StructureSummary = summary,
                        cts.Token))
                    {
                        lock (file.LockObj)
                        {
                            file.OutputBuffer.Append(chunk);
                        }
                    }
                    file.Status = AnalysisFileStatus.Done;
                }
                catch (OperationCanceledException)
                {
                    file.Status = AnalysisFileStatus.Error;
                    file.ErrorMessage = "Cancelled";
                }
                catch (Exception ex)
                {
                    file.Status = AnalysisFileStatus.Error;
                    file.ErrorMessage = ex.Message;
                    lock (file.LockObj)
                    {
                        file.OutputBuffer.AppendLine($"\n[Analysis error: {ex.Message}]");
                    }
                }
                finally
                {
                    throttler.Release();
                }
            }).ToList();

            var allTasks = Task.WhenAll(workerTasks);

            await AnsiConsole.Live(BuildView())
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        // Handle user keyboard navigation
                        while (Console.KeyAvailable)
                        {
                            var keyInfo = Console.ReadKey(intercept: true);
                            var currentFile = _files[_activeTabIndex];

                            int currentWindowHeight = 25;
                            try { currentWindowHeight = Console.WindowHeight; } catch { }
                            if (currentWindowHeight <= 0) currentWindowHeight = 25;
                            int curViewportHeight = Math.Max(5, currentWindowHeight - 7);

                            if (keyInfo.Key is ConsoleKey.RightArrow or ConsoleKey.Tab)
                            {
                                _activeTabIndex = (_activeTabIndex + 1) % _files.Count;
                            }
                            else if (keyInfo.Key == ConsoleKey.LeftArrow)
                            {
                                _activeTabIndex = (_activeTabIndex - 1 + _files.Count) % _files.Count;
                            }
                            else if (keyInfo.Key >= ConsoleKey.D1 && keyInfo.Key < ConsoleKey.D1 + Math.Min(9, _files.Count))
                            {
                                _activeTabIndex = keyInfo.Key - ConsoleKey.D1;
                            }
                            else if (keyInfo.Key == ConsoleKey.UpArrow)
                            {
                                currentFile.AutoScroll = false;
                                currentFile.ScrollOffset = Math.Max(0, currentFile.ScrollOffset - 1);
                            }
                            else if (keyInfo.Key == ConsoleKey.DownArrow)
                            {
                                currentFile.ScrollOffset++;
                            }
                            else if (keyInfo.Key == ConsoleKey.PageUp)
                            {
                                currentFile.AutoScroll = false;
                                currentFile.ScrollOffset = Math.Max(0, currentFile.ScrollOffset - Math.Max(1, curViewportHeight - 2));
                            }
                            else if (keyInfo.Key == ConsoleKey.PageDown)
                            {
                                currentFile.ScrollOffset += Math.Max(1, curViewportHeight - 2);
                            }
                            else if (keyInfo.Key == ConsoleKey.Home)
                            {
                                currentFile.AutoScroll = false;
                                currentFile.ScrollOffset = 0;
                            }
                            else if (keyInfo.Key == ConsoleKey.End)
                            {
                                currentFile.AutoScroll = true;
                            }
                            else if (keyInfo.Key is ConsoleKey.Q or ConsoleKey.Escape)
                            {
                                cts.Cancel();
                                break;
                            }
                            else if (keyInfo.Key == ConsoleKey.Enter && allTasks.IsCompleted)
                            {
                                return;
                            }
                        }

                        ctx.UpdateTarget(BuildView());
                        ctx.Refresh();

                        if (allTasks.IsCompleted)
                        {
                            // If user is done or all are finished, allow viewing or exit on Enter/Q
                            if (!Console.KeyAvailable)
                            {
                                await Task.Delay(100, CancellationToken.None);
                            }
                        }
                        else
                        {
                            await Task.Delay(40, CancellationToken.None);
                        }
                    }
                });

            try
            {
                await allTasks;
            }
            catch (OperationCanceledException)
            {
                // Graceful cancellation
            }
        }

        private async Task RunSequentialFallbackAsync(CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[grey]Non-interactive environment detected; running analyses with progress reporting...[/]");
            using var throttler = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

            var tasks = _files.Select(async file =>
            {
                await throttler.WaitAsync(cancellationToken);
                try
                {
                    file.Status = AnalysisFileStatus.Analyzing;
                    AnsiConsole.MarkupLine($"[cyan]Starting:[/] {file.FileName}");

                    await foreach (var chunk in _agent.AnalyzeStreamAsync(
                        file.FilePath,
                        summary => file.StructureSummary = summary,
                        cancellationToken))
                    {
                        lock (file.LockObj)
                        {
                            file.OutputBuffer.Append(chunk);
                        }
                    }
                    file.Status = AnalysisFileStatus.Done;
                    AnsiConsole.MarkupLine($"[green]Finished:[/] {file.FileName}");
                }
                catch (Exception ex)
                {
                    file.Status = AnalysisFileStatus.Error;
                    file.ErrorMessage = ex.Message;
                    AnsiConsole.MarkupLine($"[red]Error on {file.FileName}:[/] {ex.Message}");
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private IRenderable BuildView()
        {
            var grid = new Grid().AddColumn();

            // 1. Build Tab Bar
            var tabBadges = new List<string>();
            for (int i = 0; i < _files.Count; i++)
            {
                var file = _files[i];
                bool isActive = i == _activeTabIndex;

                string statusText = file.Status switch
                {
                    AnalysisFileStatus.Pending => "...",
                    AnalysisFileStatus.Analyzing => "~",
                    AnalysisFileStatus.Done => "OK",
                    AnalysisFileStatus.Error => "ERR",
                    _ => ""
                };

                string safeName = Markup.Escape(file.FileName);
                int numKey = i + 1;

                if (isActive)
                {
                    tabBadges.Add($"[black on cyan]  {numKey}. {safeName} ({statusText})  [/]");
                }
                else
                {
                    string color = file.Status switch
                    {
                        AnalysisFileStatus.Done => "green",
                        AnalysisFileStatus.Analyzing => "yellow",
                        AnalysisFileStatus.Error => "red",
                        _ => "grey"
                    };
                    tabBadges.Add($"[{color}] {numKey}. {safeName} ({statusText}) [/]");
                }
            }

            var tabRow = new Markup(string.Join(" ", tabBadges));
            grid.AddRow(tabRow);

            // 2. Instructions bar
            var navHint = new Text("[Tab/Left/Right/1-9] Tabs   [Up/Down/PgUp/PgDn] Scroll   [End] Auto-scroll   [Q/Esc] Exit\n", new Style(Color.Grey));
            grid.AddRow(navHint);

            // 3. Active Tab Panel
            var activeFile = _files[_activeTabIndex];
            string content;
            lock (activeFile.LockObj)
            {
                content = activeFile.OutputBuffer.ToString();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                content = activeFile.Status switch
                {
                    AnalysisFileStatus.Pending => "Queued, waiting for inference worker...",
                    AnalysisFileStatus.Analyzing => "Parsing AST and generating review with local LLM...",
                    _ => "No content."
                };
            }

            int windowHeight = 25;
            int windowWidth = 80;
            try
            {
                windowHeight = Console.WindowHeight;
                windowWidth = Console.WindowWidth;
            }
            catch { }

            if (windowHeight <= 0) windowHeight = 25;
            if (windowWidth <= 0) windowWidth = 80;

            // Reserve rows for: Tab Bar (1), Nav Hint (2), Panel Borders and Header (3)
            int viewportHeight = Math.Max(5, windowHeight - 7);
            int usableWidth = Math.Max(20, windowWidth - 6);

            var wrappedLines = new List<string>();
            foreach (var rawLine in content.Replace("\r\n", "\n").Split('\n'))
            {
                if (rawLine.Length <= usableWidth)
                {
                    wrappedLines.Add(rawLine);
                }
                else
                {
                    for (int i = 0; i < rawLine.Length; i += usableWidth)
                    {
                        wrappedLines.Add(rawLine.Substring(i, Math.Min(usableWidth, rawLine.Length - i)));
                    }
                }
            }

            int totalLines = wrappedLines.Count;
            int maxOffset = Math.Max(0, totalLines - viewportHeight);

            if (activeFile.AutoScroll)
            {
                activeFile.ScrollOffset = maxOffset;
            }
            else
            {
                activeFile.ScrollOffset = Math.Clamp(activeFile.ScrollOffset, 0, maxOffset);
                if (activeFile.ScrollOffset >= maxOffset && activeFile.Status == AnalysisFileStatus.Analyzing)
                {
                    activeFile.AutoScroll = true;
                }
            }

            var visibleLines = wrappedLines.Skip(activeFile.ScrollOffset).Take(viewportHeight);
            string visibleText = string.Join("\n", visibleLines);

            string statusDescription = activeFile.Status switch
            {
                AnalysisFileStatus.Pending => "Queued",
                AnalysisFileStatus.Analyzing => "Analyzing (Streaming)",
                AnalysisFileStatus.Done => "Analysis Complete",
                AnalysisFileStatus.Error => "Error",
                _ => ""
            };

            string scrollIndicator = totalLines > viewportHeight
                ? (activeFile.AutoScroll
                    ? "[grey](Auto-scroll)[/]"
                    : $"[yellow](Lines {activeFile.ScrollOffset + 1}-{Math.Min(activeFile.ScrollOffset + viewportHeight, totalLines)}/{totalLines})[/]")
                : "";

            string safeActiveName = Markup.Escape(activeFile.FileName);
            string headerText = $"[bold]{safeActiveName}[/] [grey]({statusDescription})[/]";
            if (!string.IsNullOrEmpty(scrollIndicator))
            {
                headerText += $" {scrollIndicator}";
            }
            if (!string.IsNullOrEmpty(activeFile.StructureSummary))
            {
                headerText += $" - [grey]{Markup.Escape(activeFile.StructureSummary)}[/]";
            }

            Color borderColor = activeFile.Status switch
            {
                AnalysisFileStatus.Analyzing => Color.Cyan1,
                AnalysisFileStatus.Done => Color.Green,
                AnalysisFileStatus.Error => Color.Red,
                _ => Color.Grey
            };

            var panel = new Panel(new Text(visibleText))
            {
                Header = new PanelHeader(headerText),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(borderColor),
                Height = viewportHeight + 2,
                Expand = true
            };

            grid.AddRow(panel);
            return grid;
        }

        private void RenderSummaryTable()
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold yellow]Multi-File Analysis Summary[/]")
                .AddColumn("File")
                .AddColumn("Status")
                .AddColumn("Structure Summary")
                .AddColumn("Recommendation Length");

            foreach (var file in _files)
            {
                string statusMarkup = file.Status switch
                {
                    AnalysisFileStatus.Done => "[green]Completed[/]",
                    AnalysisFileStatus.Error => $"[red]Error: {Markup.Escape(file.ErrorMessage ?? "Unknown")}[/]",
                    AnalysisFileStatus.Analyzing => "[yellow]Interrupted[/]",
                    _ => "[grey]Pending[/]"
                };

                int charCount;
                lock (file.LockObj)
                {
                    charCount = file.OutputBuffer.Length;
                }

                table.AddRow(
                    new Markup($"[bold]{Markup.Escape(file.FileName)}[/]"),
                    new Markup(statusMarkup),
                    new Text(file.StructureSummary ?? "N/A"),
                    new Text($"{charCount:N0} chars")
                );
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);
        }
    }
}

