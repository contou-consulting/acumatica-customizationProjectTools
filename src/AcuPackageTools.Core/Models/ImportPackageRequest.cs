using System.Text.Json.Serialization;

namespace AcuPackageTools.Models
{
    public record ImportPackageRequest(
        int? ProjectLevel,
        bool IsReplaceIfExists,
        string ProjectName,
        string ProjectDescription,
        string ProjectContentBase64
    )
    {
        [JsonPropertyName("projectLevel")]
        public int? ProjectLevel { get; } = ProjectLevel;

        [JsonPropertyName("isReplaceIfExists")]
        public bool IsReplaceIfExists { get; } = IsReplaceIfExists;

        [JsonPropertyName("projectName")]
        public string ProjectName { get; } = ProjectName;

        [JsonPropertyName("projectDescription")]
        public string ProjectDescription { get; } = ProjectDescription;

        [JsonPropertyName("projectContentBase64")]
        public string ProjectContentBase64 { get; } = ProjectContentBase64;
    }
}