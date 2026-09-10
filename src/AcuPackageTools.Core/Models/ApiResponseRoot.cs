using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcuPackageTools.Models;

public class ApiResponseRoot
{
    [JsonPropertyName("log")]
    public List<Log> Log { get; set; }
}

public class Log
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("logType")]
    public string LogType { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}
