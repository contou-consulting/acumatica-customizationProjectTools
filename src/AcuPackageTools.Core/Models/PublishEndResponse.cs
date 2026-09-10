using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcuPackageTools.Models;

public class PublishEndResponse
{
    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }

    [JsonPropertyName("isFailed")]
    public bool IsFailed { get; set; }

    [JsonPropertyName("log")]
    public List<Log> Log { get; set; }
}
