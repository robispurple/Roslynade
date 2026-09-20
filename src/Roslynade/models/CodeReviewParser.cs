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

            // 1. Check for markdown code fence ```json ... ``` or ``` ... ```
            var match = Regex.Match(trimmed, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                trimmed = match.Groups[1].Value.Trim();
            }

            // 2. Extract substring between first '{' and last '}'
            int firstBrace = trimmed.IndexOf('{');
            int lastBrace = trimmed.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return trimmed;
        }
    }
}
