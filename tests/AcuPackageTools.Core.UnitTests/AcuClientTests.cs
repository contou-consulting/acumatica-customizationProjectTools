using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AcuPackageTools.Models;
using Xunit;

namespace AcuPackageTools.Core.UnitTests;

public class AcuClientTests
{
    private const string BaseUrl = "https://example.com/instance";

    private static (AcuClient Client, FakeHttpMessageHandler Handler) CreateClient(string username = "admin")
    {
        var handler = new FakeHttpMessageHandler();
        var client = new AcuClient(new HttpClient(handler), new AcuClientOptions
        {
            Url = BaseUrl,
            Username = username,
            Password = "pw",
            Tenant = "Company"
        });
        return (client, handler);
    }

    [Fact]
    public void CreateHttpClient_HasInfiniteTimeout_AndJsonAcceptHeader()
    {
        using var client = AcuClient.CreateHttpClient();
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
        Assert.Contains(client.DefaultRequestHeaders.Accept, h => h.MediaType == "application/json");
    }

    [Fact]
    public async Task LoginAsync_PostsCredentials()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{}");

        await client.LoginAsync();

        var (request, content) = Assert.Single(handler.Requests);
        Assert.Equal("/instance/entity/auth/login", request.RequestUri.AbsolutePath);
        using var sent = JsonDocument.Parse(content);
        Assert.Equal("admin", sent.RootElement.GetProperty("name").GetString());
        Assert.Equal("pw", sent.RootElement.GetProperty("password").GetString());
        Assert.Equal("Company", sent.RootElement.GetProperty("company").GetString());
    }

    [Fact]
    public async Task LoginAsync_Failure_ThrowsAcuApiException_WithStatusAndRawBody()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.Unauthorized, "denied");

        var ex = await Assert.ThrowsAsync<AcuApiException>(() => client.LoginAsync());

        Assert.Equal($"Failed to connect to {BaseUrl} (HTTP 401): denied", ex.Message);
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("denied", ex.ResponseContent);
    }

    [Fact]
    public async Task LoginAsync_WithoutCredentials_Throws()
    {
        var handler = new FakeHttpMessageHandler();
        var client = new AcuClient(new HttpClient(handler), new AcuClientOptions { Url = BaseUrl });

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.LoginAsync());
    }

    [Fact]
    public async Task LogoutAsync_PostsEmptyBody()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "");

        await client.LogoutAsync();

        var (request, content) = Assert.Single(handler.Requests);
        Assert.Equal("/instance/entity/auth/logout", request.RequestUri.AbsolutePath);
        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public async Task ImportPackageAsync_PostsToImportEndpoint_ReturnsLogs()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK,
            "{\"log\":[{\"timestamp\":\"2024-01-01T00:00:00\",\"logType\":\"information\",\"message\":\"done\"}]}");

        var result = await client.ImportPackageAsync(new ImportPackageRequest(1, true, "Pkg", "descr", "AAAA"));

        Assert.Equal("/instance/CustomizationApi/import", handler.Requests[0].Request.RequestUri.AbsolutePath);
        var log = Assert.Single(result.Log);
        Assert.Equal("done", log.Message);
    }

    [Fact]
    public async Task GetPublishedAsync_DeserializesProjectsAndItems()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK,
            "{\"projects\":[{\"name\":\"PkgA\"}],\"items\":[{\"key\":\"K\",\"screenId\":\"SO301000\",\"type\":\"T\"}],\"log\":[]}");

        var result = await client.GetPublishedAsync();

        Assert.Equal("/instance/CustomizationApi/getPublished", handler.Requests[0].Request.RequestUri.AbsolutePath);
        Assert.Equal("PkgA", Assert.Single(result.Projects).Name);
        Assert.Equal("SO301000", Assert.Single(result.Items).ScreenId);
    }

    [Fact]
    public async Task GetProjectAsync_DeserializesContentAndConflicts()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK,
            "{\"projectContentBase64\":\"QQ==\",\"hasConflicts\":true,\"log\":[]}");

        var result = await client.GetProjectAsync(new GetProjectRequest("Pkg", true));

        Assert.Equal("/instance/CustomizationApi/getProject", handler.Requests[0].Request.RequestUri.AbsolutePath);
        Assert.Equal("QQ==", result.ProjectContentBase64);
        Assert.True(result.HasConflicts);
    }

    [Fact]
    public async Task DeleteProjectAsync_PostsProjectName()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{\"log\":[]}");

        await client.DeleteProjectAsync(new DeletePackageRequest("Pkg"));

        var (request, content) = Assert.Single(handler.Requests);
        Assert.Equal("/instance/CustomizationApi/delete", request.RequestUri.AbsolutePath);
        using var sent = JsonDocument.Parse(content);
        Assert.Equal("Pkg", sent.RootElement.GetProperty("projectName").GetString());
    }

    [Fact]
    public async Task UnpublishAllAsync_PostsToUnpublishEndpoint()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{\"log\":[]}");

        await client.UnpublishAllAsync(new UnpublishAllRequest(TenantMode.Current, null));

        Assert.Equal("/instance/CustomizationApi/unpublishAll", handler.Requests[0].Request.RequestUri.AbsolutePath);
    }
}
