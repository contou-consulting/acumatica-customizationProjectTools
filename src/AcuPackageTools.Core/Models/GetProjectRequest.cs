using System.Text.Json.Serialization;

namespace AcuPackageTools.Models;

public record GetProjectRequest(string ProjectName, bool IsAutoResolveConflicts)
{
    [JsonPropertyName("projectName")]
    public string ProjectName { get; } = ProjectName;

    [JsonPropertyName("isAutoResolveConflicts")]
    public bool IsAutoResolveConflicts { get; } = IsAutoResolveConflicts;
}
