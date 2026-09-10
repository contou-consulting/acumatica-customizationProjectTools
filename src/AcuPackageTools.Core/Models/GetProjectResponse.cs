using System.Text.Json.Serialization;

namespace AcuPackageTools.Models;

public class GetProjectResponse
{
    [JsonPropertyName("projectContentBase64")]
    public string ProjectContentBase64 { get; set; }

    [JsonPropertyName("hasConflicts")]
    public bool HasConflicts { get; set; }

    [JsonPropertyName("log")]
    public Log[] Log { get; set; }
}
