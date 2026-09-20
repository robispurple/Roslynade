using System.Text.Json.Serialization;

namespace Roslynade.Models
{
    public record FoundryModelCatalogResponse
    {
        [JsonPropertyName("models")]
        public IReadOnlyList<FoundryModelInfo> Models { get; init; } = [];
    }

    public record FoundryModelInfo
    {
        [JsonPropertyName("alias")]
        public required string Alias { get; init; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("device")]
        public string Device { get; init; } = string.Empty;

        [JsonPropertyName("fileSizeMb")]
        public long FileSizeMb { get; init; }

        [JsonPropertyName("cached")]
        public bool Cached { get; init; }

        [JsonPropertyName("license")]
        public string? License { get; init; }

        [JsonPropertyName("supportsToolCalling")]
        public bool SupportsToolCalling { get; init; }

        public string FormattedSize => FileSizeMb >= 1024
            ? $"{FileSizeMb / 1024.0:F1} GB"
            : $"{FileSizeMb} MB";
    }
}

