using System.Text.Json.Serialization;

namespace Roslynade.Models
{
    public class FoundryModelCatalogResponse
    {
        [JsonPropertyName("models")]
        public List<FoundryModelInfo> Models { get; set; } = new();
    }

    public class FoundryModelInfo
    {
        [JsonPropertyName("alias")]
        public string Alias { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("device")]
        public string Device { get; set; } = string.Empty;

        [JsonPropertyName("fileSizeMb")]
        public long FileSizeMb { get; set; }

        [JsonPropertyName("cached")]
        public bool Cached { get; set; }

        [JsonPropertyName("license")]
        public string? License { get; set; }

        [JsonPropertyName("supportsToolCalling")]
        public bool SupportsToolCalling { get; set; }

        public string FormattedSize => FileSizeMb >= 1024
            ? $"{FileSizeMb / 1024.0:F1} GB"
            : $"{FileSizeMb} MB";
    }
}

