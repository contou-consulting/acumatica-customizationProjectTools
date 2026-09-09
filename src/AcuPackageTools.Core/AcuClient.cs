using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AcuPackageTools.Models;

namespace AcuPackageTools;

/// <summary>
/// Client for the Acumatica Customization API. All operations are async and
/// honor cancellation; the request/response wire behavior (URL construction,
/// serializer options per endpoint, error message text) intentionally matches
/// the AcuPackageTools PowerShell module, which wraps this client.
/// </summary>
public sealed class AcuClient : IDisposable
{
    // Kept in sync with ApiCmdlet.SerializerOptions in the PowerShell module,
    // which retains its own copy for the Connect-AcuInstance cmdlet.
    public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly bool _ownsHttpClient;
    private readonly string _username;
    private readonly string _password;
    private bool _disposed;

    /// <summary>Creates a client that owns its HttpClient and can log in with the configured credentials.</summary>
    public AcuClient(AcuClientOptions options)
        : this(options, httpClient: null)
    {
    }

    /// <summary>
    /// Wraps an existing HttpClient (e.g. one already carrying an authenticated
    /// cookie session) without taking ownership of it.
    /// </summary>
    public AcuClient(HttpClient httpClient, AcuClientOptions options)
        : this(options, httpClient ?? throw new ArgumentNullException(nameof(httpClient)))
    {
    }

    private AcuClient(AcuClientOptions options, HttpClient httpClient)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrEmpty(options.Url)) throw new ArgumentException("Url is required.", nameof(options));

        Url = options.Url;
        Tenant = options.Tenant;
        _username = options.Username;
        _password = options.Password;
        _ownsHttpClient = httpClient == null;
        HttpClient = httpClient ?? CreateHttpClient(options.SkipCertificateCheck);
    }

    public string Url { get; }
    public string Tenant { get; }
    public HttpClient HttpClient { get; }

    /// <summary>Diagnostic message sink; null means silent. May be invoked on thread-pool threads.</summary>
    public Action<string> Verbose { get; set; }

    public static HttpClient CreateHttpClient(bool skipCertificateCheck = false)
    {
        var handler = new HttpClientHandler()
        {
            CookieContainer = new CookieContainer()
        };
        if (skipCertificateCheck)
        {
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }
        var client = new HttpClient(handler, true);
        // Publish requests (publishBegin) can legitimately run longer than the
        // 100-second HttpClient default; completion is tracked by polling publishEnd.
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<JsonDocument> SendRequestAsync(string resource, object body = null, CancellationToken cancellationToken = default)
    {
        var uriBuilder = new UriBuilder(Url);
        uriBuilder.Path += resource;
        var url = uriBuilder.ToString();
        HttpResponseMessage response;
        if (body is null)
        {
            response = await HttpClient.PostAsync(url, new StringContent(string.Empty, Encoding.UTF8, "application/json"), cancellationToken)
                                       .ConfigureAwait(false);
            Verbose?.Invoke("Posting Empty Body to " + url);
        }
        else
        {
            string requestContent = JsonSerializer.Serialize(body, SerializerOptions);
            response = await HttpClient.PostAsync(url, new StringContent(requestContent, Encoding.UTF8, "application/json"), cancellationToken)
                                       .ConfigureAwait(false);
            Verbose?.Invoke($"Posting content to " + url);
            Verbose?.Invoke(requestContent);
        }

        string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        JsonDocument jDoc = string.IsNullOrWhiteSpace(responseContent)
            ? default
            : JsonDocument.Parse(responseContent);
        if (!response.IsSuccessStatusCode
         && !string.IsNullOrWhiteSpace(responseContent))
            throw new HttpRequestException(
                $"There was a failure when calling {url} (HTTP {(int)response.StatusCode}): "
              + Environment.NewLine
              + JsonSerializer.Serialize(jDoc, SerializerOptions));

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"There was a failure when calling {url} (HTTP {(int)response.StatusCode})");
        }

        Verbose?.Invoke(JsonSerializer.Serialize(jDoc, SerializerOptions));

        return jDoc;
    }

    /// <summary>
    /// Logs in with the credentials from <see cref="AcuClientOptions"/>. On
    /// failure throws <see cref="AcuApiException"/> carrying the status code
    /// and raw response body.
    /// </summary>
    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        if (_username == null)
            throw new InvalidOperationException("No credentials configured. Construct the client with AcuClientOptions to use LoginAsync.");

        var loginRequest = new LoginRequest(_username, _password, Tenant);
        var uriBuilder = new UriBuilder(Url);
        uriBuilder.Path += AcuEndpoints.Login;
        var loginUrl = uriBuilder.ToString();

        var requestContent = JsonSerializer.Serialize(loginRequest, SerializerOptions);
        var response = await HttpClient.PostAsync(loginUrl,
            new StringContent(requestContent, Encoding.UTF8, "application/json"), cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new AcuApiException(
                $"Failed to connect to {Url} (HTTP {(int)response.StatusCode}): {errorContent}",
                (int)response.StatusCode,
                errorContent);
        }
    }

    /// <summary>Posts a logout; the response is ignored. Network failures propagate.</summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var uriBuilder = new UriBuilder(Url);
        uriBuilder.Path += AcuEndpoints.Logout;
        await HttpClient.PostAsync(uriBuilder.ToString(),
            new StringContent(string.Empty, Encoding.UTF8, "application/json"), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApiResponseRoot> ImportPackageAsync(ImportPackageRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendRequestAsync(AcuEndpoints.Import, request, cancellationToken).ConfigureAwait(false);
        return response.Deserialize<ApiResponseRoot>();
    }

    public async Task<GetProjectResponse> GetProjectAsync(GetProjectRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendRequestAsync(AcuEndpoints.GetProject, request, cancellationToken).ConfigureAwait(false);
        return response.Deserialize<GetProjectResponse>();
    }

    public async Task<GetPublishedResponse> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendRequestAsync(AcuEndpoints.GetPublished, null, cancellationToken).ConfigureAwait(false);
        return response.Deserialize<GetPublishedResponse>();
    }

    public async Task<ApiResponseRoot> DeleteProjectAsync(DeletePackageRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendRequestAsync(AcuEndpoints.Delete, request, cancellationToken).ConfigureAwait(false);
        return response.Deserialize<ApiResponseRoot>();
    }

    public async Task UnpublishAllAsync(UnpublishAllRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendRequestAsync(AcuEndpoints.UnpublishAll, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a publish and polls publishEnd until the operation completes or
    /// fails. Every log entry not yet seen (deduplicated by timestamp) is passed
    /// to <paramref name="onLog"/>; <paramref name="onPollTick"/> receives the
    /// 1-based poll counter. A failed publish returns normally with
    /// <see cref="PublishEndResponse.IsFailed"/> set — the caller decides how to
    /// surface it.
    /// </summary>
    public async Task<PublishEndResponse> PublishAsync(
        PublishBeginRequest request,
        Action<Log> onLog = null,
        Action<int> onPollTick = null,
        int pollIntervalMs = 1000,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var startResponse = await SendRequestAsync(AcuEndpoints.PublishBegin, request, cancellationToken).ConfigureAwait(false);
        startResponse?.Dispose();

        HashSet<DateTime> existingTimeStamps = new HashSet<DateTime>();
        int elapsedSeconds = 0;
        Stopwatch stopwatch = timeout.HasValue ? Stopwatch.StartNew() : null;

        PublishEndResponse responseData;
        do
        {
            using (var endResponse = await SendRequestAsync(AcuEndpoints.PublishEnd, null, cancellationToken).ConfigureAwait(false))
            {
                responseData = JsonSerializer.Deserialize<PublishEndResponse>(
                    endResponse.RootElement.GetRawText(), SerializerOptions);
            }

            foreach (var log in responseData.Log)
            {
                if (existingTimeStamps.Contains(log.Timestamp)) continue;
                onLog?.Invoke(log);
                existingTimeStamps.Add(log.Timestamp);
            }

            elapsedSeconds++;
            onPollTick?.Invoke(elapsedSeconds);

            if (!responseData.IsCompleted && !responseData.IsFailed)
            {
                if (stopwatch != null && stopwatch.Elapsed > timeout.Value)
                    throw new TimeoutException($"Publish did not complete within {timeout.Value}.");
                await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
            }
        } while (!responseData.IsCompleted && !responseData.IsFailed);

        return responseData;
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_ownsHttpClient)
        {
            HttpClient.Dispose();
        }
        _disposed = true;
    }
}
