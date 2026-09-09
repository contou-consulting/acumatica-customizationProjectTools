using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcuPackageTools.Models;

public class GetPublishedResponse
{
    [JsonPropertyName("projects")]
    public List<PublishedProject> Projects { get; set; }

    [JsonPropertyName("items")]
    public List<PublishedItem> Items { get; set; }

    [JsonPropertyName("log")]
    public List<Log> Log { get; set; }
}

public class PublishedProject
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class PublishedItem
{
    [JsonPropertyName("key")]
    public string Key { get; set; }

    [JsonPropertyName("screenId")]
    public string ScreenId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }
}
