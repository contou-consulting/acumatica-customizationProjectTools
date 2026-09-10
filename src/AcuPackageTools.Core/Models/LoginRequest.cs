using System.Text.Json.Serialization;

namespace AcuPackageTools.Models;

public record LoginRequest(string Username, string Password, string Company)
{
    [JsonPropertyName("name")]
    public string Username { get; } = Username;

    [JsonPropertyName("password")]
    public string Password { get; } = Password;

    [JsonPropertyName("company")]
    public string Company { get; } = Company;
}