using System.Text.Json.Serialization;

namespace Roslynade.Models
{
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
        public string Severity { get; init; } = "Suggestion";

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

