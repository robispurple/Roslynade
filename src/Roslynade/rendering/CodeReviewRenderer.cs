using Roslynade.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Roslynade.Rendering
{
    public static class CodeReviewRenderer
    {
        public static (int Errors, int Warnings, int Suggestions) GetSeverityCounts(CodeReviewResult review)
        {
            int errors = 0, warnings = 0, suggestions = 0;
            foreach (var issue in review.Issues)
            {
                switch (issue.Severity.Trim().ToLowerInvariant())
                {
                    case "error":
                        errors++;
                        break;
                    case "warning":
                    case "warn":
                        warnings++;
                        break;
                    default:
                        suggestions++;
                        break;
                }
            }
            return (errors, warnings, suggestions);
        }

        public static List<string> FormatScrollableMarkupLines(CodeReviewResult review, int usableWidth)
        {
            var lines = new List<string>();

            // 1. Overall Score Header
            string scoreColor = review.OverallScore >= 80 ? "green" : review.OverallScore >= 60 ? "yellow" : "red";
            var (errors, warnings, suggestions) = GetSeverityCounts(review);

            string scoreLine = $"[bold]Health Score:[/] [{scoreColor} bold]{review.OverallScore}/100[/]  " +
                              $"[grey]|[/]  [red]{errors} Error(s)[/]  " +
                              $"[grey]|[/]  [yellow]{warnings} Warning(s)[/]  " +
                              $"[grey]|[/]  [cyan]{suggestions} Suggestion(s)[/]";
            lines.Add(scoreLine);
            lines.Add(string.Empty);

            // 2. Summary Section
            lines.Add("[bold cyan]── Summary ──────────────────────────────────────────[/]");
            if (!string.IsNullOrWhiteSpace(review.Summary))
            {
                foreach (var line in WrapText(Markup.Escape(review.Summary.Trim()), usableWidth))
                {
                    lines.Add($"[italic]{line}[/]");
                }
            }
            else
            {
                lines.Add("[grey]No summary provided.[/]");
            }
            lines.Add(string.Empty);

            // 3. Issues Section
            lines.Add($"[bold yellow]── Findings ({review.Issues.Count}) ────────────────────────────────[/]");

            if (review.Issues.Count == 0)
            {
                lines.Add("[green bold]✔ No issues detected. Clean and idiomatic code![/]");
            }
            else
            {
                for (int i = 0; i < review.Issues.Count; i++)
                {
                    var issue = review.Issues[i];
                    string severityBadge = issue.Severity.Trim().ToLowerInvariant() switch
                    {
                        "error" => "[white on red bold] ERROR [/]",
                        "warning" or "warn" => "[black on yellow bold] WARN [/]",
                        _ => "[black on cyan bold] SUGG [/]"
                    };

                    string lineText = issue.Line.HasValue ? $"[grey]Line {issue.Line}:[/] " : "";
                    string titleText = $"[bold]{Markup.Escape(issue.Title)}[/]";
                    lines.Add($"{severityBadge} {lineText}{titleText}");

                    if (!string.IsNullOrWhiteSpace(issue.Description))
                    {
                        foreach (var descLine in WrapText(Markup.Escape(issue.Description.Trim()), Math.Max(20, usableWidth - 4)))
                        {
                            lines.Add($"  [grey]{descLine}[/]");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(issue.SuggestedFix))
                    {
                        lines.Add("  [grey bold]Suggested Fix:[/]");
                        foreach (var fixLine in issue.SuggestedFix.Replace("\r\n", "\n").Split('\n'))
                        {
                            lines.Add($"    [green]{Markup.Escape(fixLine)}[/]");
                        }
                    }

                    if (i < review.Issues.Count - 1)
                    {
                        lines.Add("  [grey]┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈[/]");
                    }
                }
            }

            return lines;
        }

        public static Table BuildIssuesTable(CodeReviewResult review, string fileName)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title($"[bold cyan]{Markup.Escape(fileName)}[/] - [grey]Findings[/]")
                .AddColumn(new TableColumn("[bold]Severity[/]").Centered())
                .AddColumn(new TableColumn("[bold]Line[/]").Centered())
                .AddColumn(new TableColumn("[bold]Issue[/]"))
                .AddColumn(new TableColumn("[bold]Details & Fix[/]"));

            foreach (var issue in review.Issues)
            {
                string severityBadge = issue.Severity.Trim().ToLowerInvariant() switch
                {
                    "error" => "[red bold]ERROR[/]",
                    "warning" or "warn" => "[yellow bold]WARN[/]",
                    _ => "[cyan]SUGG[/]"
                };

                string lineDisplay = issue.Line.HasValue ? $"L{issue.Line}" : "-";

                var detailsItems = new List<IRenderable>
                {
                    new Markup(Markup.Escape(issue.Description))
                };

                if (!string.IsNullOrWhiteSpace(issue.SuggestedFix))
                {
                    detailsItems.Add(new Panel(new Text(issue.SuggestedFix))
                    {
                        Header = new PanelHeader("[grey]Fix[/]"),
                        Border = BoxBorder.None,
                        Expand = false
                    });
                }

                var details = new Rows(detailsItems);

                table.AddRow(
                    new Markup(severityBadge),
                    new Markup($"[grey]{lineDisplay}[/]"),
                    new Markup($"[bold]{Markup.Escape(issue.Title)}[/]"),
                    details
                );
            }

            return table;
        }

        public static IEnumerable<string> WrapText(string text, int maxWidth)
        {
            if (maxWidth <= 0) maxWidth = 80;
            var words = text.Split(' ');
            var currentLine = new System.Text.StringBuilder();

            foreach (var word in words)
            {
                if (currentLine.Length + word.Length + 1 > maxWidth && currentLine.Length > 0)
                {
                    yield return currentLine.ToString();
                    currentLine.Clear();
                }

                if (currentLine.Length > 0) currentLine.Append(' ');
                currentLine.Append(word);
            }

            if (currentLine.Length > 0)
            {
                yield return currentLine.ToString();
            }
        }
    }
}
