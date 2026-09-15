using Roslynade.Models;
using Roslynade.Rendering;
using Spectre.Console;

namespace Roslynade.Tests
{
    public class CodeReviewTests
    {
        [Fact]
        public void Parse_PureJson_Succeeds()
        {
            string json = """
            {
              "summary": "Clean code with minor suggestions.",
              "overallScore": 92,
              "issues": [
                {
                  "severity": "Warning",
                  "line": 15,
                  "title": "Use pattern matching",
                  "description": "Consider using 'is null' instead of '== null'.",
                  "suggestedFix": "if (x is null)"
                }
              ]
            }
            """;

            bool success = CodeReviewParser.TryParse(json, out var result);

            Assert.True(success);
            Assert.NotNull(result);
            Assert.Equal("Clean code with minor suggestions.", result.Summary);
            Assert.Equal(92, result.OverallScore);
            Assert.Single(result.Issues);
            Assert.Equal(15, result.Issues[0].Line);
            Assert.Equal("Warning", result.Issues[0].Severity);
        }

        [Fact]
        public void Parse_MarkdownFencedJson_Succeeds()
        {
            string fenced = """
            Here is the analysis:
            ```json
            {
              "summary": "Code looks good overall.",
              "overallScore": 88,
              "issues": []
            }
            ```
            Hope this helps!
            """;

            bool success = CodeReviewParser.TryParse(fenced, out var result);

            Assert.True(success);
            Assert.NotNull(result);
            Assert.Equal(88, result.OverallScore);
            Assert.Empty(result.Issues);
        }

        [Fact]
        public void Parse_InvalidJson_ReturnsFalse()
        {
            string bad = "This is not json at all.";
            bool success = CodeReviewParser.TryParse(bad, out var result);

            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void Render_DangerousMarkupInIssues_EscapesProperlyWithoutCrashing()
        {
            var review = new CodeReviewResult
            {
                Summary = "Check collection indexing: array[0] and List<string> with [Fact] attributes.",
                OverallScore = 75,
                Issues = new List<CodeIssue>
                {
                    new()
                    {
                        Severity = "Error",
                        Line = 42,
                        Title = "Unsafe index access on items[i]",
                        Description = "Index items[i] may throw IndexOutOfRangeException if count < [10]. Use List<T>.TryGetValue or pattern matching [0..^1].",
                        SuggestedFix = "if (items.Length > 0) return items[0];"
                    }
                }
            };

            // Formatting markup lines should not throw InvalidOperationException
            var lines = CodeReviewRenderer.FormatScrollableMarkupLines(review, 80);

            Assert.NotEmpty(lines);

            // Verify each line can be parsed by Spectre's Markup parser without throwing
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var markup = new Markup(line);
                    Assert.NotNull(markup);
                }
            }

            // Verify table builds without throwing
            var table = CodeReviewRenderer.BuildIssuesTable(review, "TestFile.cs");
            Assert.NotNull(table);
        }

        [Fact]
        public void Parse_UserModelResponse_Succeeds()
        {
            string userOutput = """
            ```json
            {
              "summary": "The code contains a static analyzer class with a method that counts syntax nodes. The implementation is syntactically valid but lacks null safety for the input parameter and could benefit from using LINQ to Count for readability. No critical bugs are present, but the code could be improved for safety and maintainability.",
              "overallScore": 82,
              "issues": [
                {
                  "severity": "Warning",
                  "line": 12,
                  "title": "Missing null check for SyntaxNode parameter",
                  "description": "The method calls root.OfType<...> without checking if 'root' is null. If 'root' is null, this will throw an exception.",
                  "suggestedFix": "Add a null check: if (root == null) return \"...\"; else return \"...\";"
                },
                {
                  "severity": "Suggestion",
                  "line": 12,
                  "title": "Consider using LINQ to Count for readability",
                  "description": "The current code uses a for-each loop to count nodes. This can be replaced with a LINQ query for more concise and readable code.",
                  "suggestedFix": "return root.DescendantNodes().OfType<...>().Count();"
                }
              ]
            }
            ```
            """;

            bool parsed = CodeReviewParser.TryParse(userOutput, out var review);

            Assert.True(parsed);
            Assert.NotNull(review);
            Assert.Equal(82, review.OverallScore);
            Assert.Equal(2, review.Issues.Count);
            Assert.Equal("Warning", review.Issues[0].Severity);
            Assert.Equal("Suggestion", review.Issues[1].Severity);

            var lines = CodeReviewRenderer.FormatScrollableMarkupLines(review, 80);
            Assert.NotEmpty(lines);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var markup = new Markup(line);
                    Assert.NotNull(markup);
                }
            }
        }
    }
}

