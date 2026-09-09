using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AcuPackageTools.Models;
using Xunit;

namespace AcuPackageTools.Core.UnitTests;

/// <summary>
/// Parity locks for AcuClient.SendRequestAsync: URL construction, verbose
/// ordering, serializer options, and the exact error behavior the PowerShell
/// module's ErrorRecords depend on.
/// </summary>
public class SendRequestAsyncTests
{
    private const string BaseUrl = "https://example.com/instance";

    private static string ExpectedUrl(string resource)
    {
        var uriBuilder = new UriBuilder(BaseUrl);
        uriBuilder.Path += resource;
        return uriBuilder.ToString();
    }

    private static (AcuClient Client, FakeHttpMessageHandler Handler, List<string> Verbose) CreateClient()
    {
        var handler = new FakeHttpMessageHandler();
        var client = new AcuClient(new HttpClient(handler), new AcuClientOptions
        {
            Url = BaseUrl,
            Username = "admin",
            Password = "pw",
            Tenant = "Company"
        });
        var verbose = new List<string>();
        client.Verbose = verbose.Add;
        return (client, handler, verbose);
    }

    [Fact]
    public async Task EmptyBody_PostsEmptyJson_VerboseAfterResponse()
    {
        var (client, handler, verbose) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{\"ok\":true}");

        using var doc = await client.SendRequestAsync("/CustomizationApi/getPublished");

        Assert.NotNull(doc);
        var (request, content) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/instance/CustomizationApi/getPublished", request.RequestUri.AbsolutePath);
        Assert.Equal(string.Empty, content);
        Assert.Equal(
            new[]
            {
                "Posting Empty Body to " + ExpectedUrl("/CustomizationApi/getPublished"),
                JsonSerializer.Serialize(JsonDocument.Parse("{\"ok\":true}"), AcuClient.SerializerOptions)
            },
            verbose);
    }

    [Fact]
    public async Task Body_SerializedWithSharedOptions_EnumAsString_NullsOmitted()
    {
        var (client, handler, verbose) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{}");

        using var doc = await client.SendRequestAsync(
            "/CustomizationApi/unpublishAll", new UnpublishAllRequest(TenantMode.All, null));

        var (_, content) = Assert.Single(handler.Requests);
        using var sent = JsonDocument.Parse(content);
        Assert.Equal("All", sent.RootElement.GetProperty("tenantMode").GetString());
        Assert.False(sent.RootElement.TryGetProperty("tenantLoginNames", out _));
        Assert.Equal("Posting content to " + ExpectedUrl("/CustomizationApi/unpublishAll"), verbose[0]);
        Assert.Equal(content, verbose[1]);
        Assert.Equal(3, verbose.Count);
    }

    [Fact]
    public async Task EmptyResponseBody_ReturnsNullDocument_VerbosesNull()
    {
        var (client, handler, verbose) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "");

        var doc = await client.SendRequestAsync("/entity/auth/logout");

        Assert.Null(doc);
        Assert.Equal("null", verbose[^1]);
    }

    [Fact]
    public async Task ErrorWithJsonBody_ThrowsHttpRequestException_ExactMessage()
    {
        var (client, handler, _) = CreateClient();
        const string body = "{\"error\":\"bad\"}";
        handler.Enqueue(HttpStatusCode.BadRequest, body);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendRequestAsync("/CustomizationApi/import", new { }));

        Assert.Equal(
            $"There was a failure when calling {ExpectedUrl("/CustomizationApi/import")} (HTTP 400): "
            + Environment.NewLine
            + JsonSerializer.Serialize(JsonDocument.Parse(body), AcuClient.SerializerOptions),
            ex.Message);
    }

    [Fact]
    public async Task ErrorWithEmptyBody_ThrowsHttpRequestException_ShortMessage()
    {
        var (client, handler, _) = CreateClient();
        handler.Enqueue(HttpStatusCode.InternalServerError, "");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendRequestAsync("/CustomizationApi/import"));

        Assert.Equal(
            $"There was a failure when calling {ExpectedUrl("/CustomizationApi/import")} (HTTP 500)",
            ex.Message);
    }

    [Fact]
    public async Task NonJsonErrorBody_ThrowsJsonException()
    {
        var (client, handler, _) = CreateClient();
        handler.Enqueue(HttpStatusCode.InternalServerError, "<html>oops</html>");

        await Assert.ThrowsAnyAsync<JsonException>(() => client.SendRequestAsync("/CustomizationApi/import"));
    }

    [Fact]
    public async Task NonJsonSuccessBody_ThrowsJsonException()
    {
        var (client, handler, _) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "not json");

        await Assert.ThrowsAnyAsync<JsonException>(() => client.SendRequestAsync("/CustomizationApi/import"));
    }
}
