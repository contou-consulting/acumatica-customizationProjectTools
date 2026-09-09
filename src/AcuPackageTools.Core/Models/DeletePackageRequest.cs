using System.Text.Json.Serialization;

namespace AcuPackageTools.Models;

public record DeletePackageRequest(string ProjectName)
{
    [JsonPropertyName("projectName")]
    public string ProjectName { get; } = ProjectName;
}
