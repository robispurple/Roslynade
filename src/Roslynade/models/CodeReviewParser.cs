using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Roslynade.Models
{
    public static partial class CodeReviewParser
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        [GeneratedRegex(@"```(?:json)?\s*", RegexOptions.IgnoreCase)]
        private static partial Regex JsonFenceRegex();

        public static bool TryParse(string rawContent, [NotNullWhen(true)] out CodeReviewResult? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return false;
            }

            try
            {
                string cleanJson = ExtractJson(rawContent);
                result = JsonSerializer.Deserialize<CodeReviewResult>(cleanJson, JsonOptions);
                return result != null && !string.IsNullOrWhiteSpace(result.Summary);
            }
            catch (JsonException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static string ExtractJson(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            ReadOnlySpan<char> span = raw.AsSpan().Trim();

            // 1. If already a JSON object starting with '{' and ending with '}', return as-is
            if (span.StartsWith("{") && span.EndsWith("}"))
            {
                return span.ToString();
            }

            int firstBrace = span.IndexOf('{');
            int lastBrace = span.LastIndexOf('}');

            // 2. Check for markdown code fence ```json ... that starts before '{'
            var enumerator = JsonFenceRegex().EnumerateMatches(span);
            if (enumerator.MoveNext())
            {
                var match = enumerator.Current;
                if (firstBrace < 0 || match.Index < firstBrace)
                {
                    int contentStart = match.Index + match.Length;
                    int lastFence = span.LastIndexOf("```");
                    if (lastFence > contentStart)
                    {
                        span = span.Slice(contentStart, lastFence - contentStart).Trim();
                    }
                    else
                    {
                        span = span.Slice(contentStart).Trim();
                    }

                    firstBrace = span.IndexOf('{');
                    lastBrace = span.LastIndexOf('}');
                }
            }

            // 3. Extract substring between first '{' and last '}'
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return span.Slice(firstBrace, lastBrace - firstBrace + 1).ToString();
            }

            return span.ToString();
        }
    }
}
