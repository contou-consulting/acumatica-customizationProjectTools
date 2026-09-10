using System.Text.Json.Serialization;

namespace AcuPackageTools.Models;

public record PublishBeginRequest(bool? IsMergeWithExistingPackages,
    bool? IsOnlyValidation,
    bool? IsOnlyDbUpdates,
    bool? IsReplayPreviouslyExecutedScripts,
    string[] ProjectNames,
    TenantMode? TenantMode,
    string[] TenantLoginNames)
{
    [JsonPropertyName("isMergeWithExistingPackages")]
    public bool? IsMergeWithExistingPackages { get; } = IsMergeWithExistingPackages;

    [JsonPropertyName("isOnlyValidation")]
    public bool? IsOnlyValidation { get; } = IsOnlyValidation;

    [JsonPropertyName("isOnlyDbUpdates")]
    public bool? IsOnlyDbUpdates { get; } = IsOnlyDbUpdates;

    [JsonPropertyName("isReplayPreviouslyExecutedScripts")]
    public bool? IsReplayPreviouslyExecutedScripts { get; } = IsReplayPreviouslyExecutedScripts;

    [JsonPropertyName("projectNames")]
    public string[] ProjectNames { get; } = ProjectNames;

    [JsonPropertyName("tenantMode")]
    public TenantMode? TenantMode { get; } = TenantMode;

    [JsonPropertyName("tenantLoginNames")]
    public string[] TenantLoginNames { get; } = TenantLoginNames;
}