using System.Net.Http;

namespace AcuPackageTools;

/// <summary>
/// Thrown by <see cref="AcuClient.LoginAsync"/> when the instance rejects the
/// login. Derives from HttpRequestException so callers that categorize by that
/// type keep working; carries the status code and raw response body
/// (netstandard2.0 HttpRequestException exposes neither).
/// </summary>
public class AcuApiException : HttpRequestException
{
    public AcuApiException(string message, int statusCode, string responseContent)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }

    public int StatusCode { get; }
    public string ResponseContent { get; }
}
