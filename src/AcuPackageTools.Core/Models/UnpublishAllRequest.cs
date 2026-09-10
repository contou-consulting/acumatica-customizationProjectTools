using System.Text.Json.Serialization;

namespace AcuPackageTools.Models;

public record UnpublishAllRequest(TenantMode? TenantMode, string[] TenantLoginNames)
{
    [JsonPropertyName("tenantLoginNames")]
    public string[] TenantLoginNames { get; } = TenantLoginNames;

    [JsonPropertyName("tenantMode")]
    public TenantMode? TenantMode { get; } = TenantMode;
}