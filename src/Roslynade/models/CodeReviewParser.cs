using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Roslynade.Models
{
    public static class CodeReviewParser
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

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
            catch
            {
                return false;
            }
        }

        public static string ExtractJson(string raw)
        {
            string trimmed = raw.Trim();

            // 1. If already a JSON object starting with '{' and ending with '}', return as-is
            if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            {
                return trimmed;
            }

            int firstBrace = trimmed.IndexOf('{');
            int lastBrace = trimmed.LastIndexOf('}');

            // 2. Check for markdown code fence ```json ... that starts before '{'
            var match = Regex.Match(trimmed, @"```(?:json)?\s*", RegexOptions.IgnoreCase);
            if (match.Success && (firstBrace < 0 || match.Index < firstBrace))
            {
                int contentStart = match.Index + match.Length;
                int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence > contentStart)
                {
                    trimmed = trimmed.Substring(contentStart, lastFence - contentStart).Trim();
                }
                else
                {
                    trimmed = trimmed.Substring(contentStart).Trim();
                }

                firstBrace = trimmed.IndexOf('{');
                lastBrace = trimmed.LastIndexOf('}');
            }

            // 3. Extract substring between first '{' and last '}'
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return trimmed;
        }
    }
}
