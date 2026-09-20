using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roslynade.Models
{
    [JsonConverter(typeof(SeverityJsonConverter))]
    public enum Severity
    {
        Error,
        Warning,
        Suggestion
    }

    public class SeverityJsonConverter : JsonConverter<Severity>
    {
        public override Severity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    return Severity.Suggestion;
                }

                ReadOnlySpan<char> span = str.AsSpan().Trim();
                if (span.Equals("error", StringComparison.OrdinalIgnoreCase))
                {
                    return Severity.Error;
                }

                if (span.Equals("warning", StringComparison.OrdinalIgnoreCase) ||
                    span.Equals("warn", StringComparison.OrdinalIgnoreCase))
                {
                    return Severity.Warning;
                }

                return Severity.Suggestion;
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int intVal))
            {
                return Enum.IsDefined(typeof(Severity), intVal) ? (Severity)intVal : Severity.Suggestion;
            }

            return Severity.Suggestion;
        }

        public override void Write(Utf8JsonWriter writer, Severity value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    public record CodeReviewResult
    {
        [JsonPropertyName("summary")]
        public string Summary { get; init; } = string.Empty;

        [JsonPropertyName("overallScore")]
        public int OverallScore { get; init; } = 100;

        [JsonPropertyName("issues")]
        public IReadOnlyList<CodeIssue> Issues { get; init; } = [];
    }

    public record CodeIssue
    {
        [JsonPropertyName("severity")]
        public Severity Severity { get; init; } = Severity.Suggestion;

        [JsonPropertyName("line")]
        public int? Line { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("suggestedFix")]
        public string? SuggestedFix { get; init; }
    }
}


