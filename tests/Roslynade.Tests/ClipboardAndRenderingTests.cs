using Roslynade.Models;
using Roslynade.Rendering;
using Roslynade.Services;
using Xunit;

namespace Roslynade.Tests
{
    public class ClipboardAndRenderingTests
    {
        [Fact]
        public void FormatPlainText_IncludesAllFieldsWithoutMarkup()
        {
            var review = new CodeReviewResult
            {
                OverallScore = 85,
                Summary = "Good overall code quality with a minor issue.",
                Issues = new List<CodeIssue>
                {
                    new()
                    {
                        Severity = "Warning",
                        Line = 42,
                        Title = "Potential Null Reference",
                        Description = "The variable might be null here.",
                        SuggestedFix = "if (x != null) { ... }"
                    }
                }
            };

            string plainText = CodeReviewRenderer.FormatPlainText(review);

            Assert.Contains("Health Score: 85/100", plainText);
            Assert.Contains("1 Warning(s)", plainText);
            Assert.Contains("Good overall code quality with a minor issue.", plainText);
            Assert.Contains("[WARN] Line 42: Potential Null Reference", plainText);
            Assert.Contains("The variable might be null here.", plainText);
            Assert.Contains("if (x != null) { ... }", plainText);
            // Verify no Spectre markup tags leaked into plain text
            Assert.DoesNotContain("[bold]", plainText);
            Assert.DoesNotContain("[/]", plainText);
            Assert.DoesNotContain("[yellow]", plainText);
        }

        [Fact]
        public void FormatPlainText_EmptyIssues_ShowsCleanMessage()
        {
            var review = new CodeReviewResult
            {
                OverallScore = 100,
                Summary = "Clean code.",
                Issues = new List<CodeIssue>()
            };

            string plainText = CodeReviewRenderer.FormatPlainText(review);

            Assert.Contains("Health Score: 100/100", plainText);
            Assert.Contains("No issues detected", plainText);
            Assert.DoesNotContain("[bold]", plainText);
            Assert.DoesNotContain("[/]", plainText);
        }

        [Fact]
        public void ClipboardService_SetText_Succeeds()
        {
            const string expected = "Roslynade Clipboard Unit Test Content - 12345";
            bool success = ClipboardService.SetText(expected);

            Assert.True(success);
        }

        [Fact]
        public void NavHintMarkup_ParsesWithoutError()
        {
            const string copyFeedback = "   [black on green bold] ✔ Copied to clipboard [/]";
            var markup = new Spectre.Console.Markup($"[grey][[Tab/Left/Right/1-9]] Tabs   [[Up/Down/PgUp/PgDn]] Scroll   [[End]] Auto-scroll   [white on darkblue bold] C [/] Copy   [[Q/Esc]] Exit[/]{copyFeedback}\n");
            Assert.NotNull(markup);

            var markupWithoutFeedback = new Spectre.Console.Markup("[grey][[Tab/Left/Right/1-9]] Tabs   [[Up/Down/PgUp/PgDn]] Scroll   [[End]] Auto-scroll   [white on darkblue bold] C [/] Copy   [[Q/Esc]] Exit[/]\n");
            Assert.NotNull(markupWithoutFeedback);

            var panelBadge = new Spectre.Console.Markup($"[white on darkblue bold]{Spectre.Console.Markup.Escape("[C] Copy")}[/]");
            Assert.NotNull(panelBadge);

            var copiedBadge = new Spectre.Console.Markup($"[black on green bold]{Spectre.Console.Markup.Escape("[✔ Copied!]")}[/]");
            Assert.NotNull(copiedBadge);
        }
    }
}
