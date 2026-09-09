using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AcuPackageTools.Models;
using Xunit;

namespace AcuPackageTools.Core.UnitTests;

public class PublishAsyncTests
{
    private const string BaseUrl = "https://example.com/instance";

    private static readonly PublishBeginRequest Request =
        new(null, null, null, null, new[] { "Pkg" }, TenantMode.Current, null);

    private static (AcuClient Client, FakeHttpMessageHandler Handler) CreateClient()
    {
        var handler = new FakeHttpMessageHandler();
        var client = new AcuClient(new HttpClient(handler), new AcuClientOptions { Url = BaseUrl });
        return (client, handler);
    }

    private static string EndResponse(bool completed, bool failed, params (string Timestamp, string Type, string Message)[] logs)
    {
        string logJson = string.Join(",", logs.Select(l =>
            $"{{\"timestamp\":\"{l.Timestamp}\",\"logType\":\"{l.Type}\",\"message\":\"{l.Message}\"}}"));
        return $"{{\"isCompleted\":{completed.ToString().ToLowerInvariant()},\"isFailed\":{failed.ToString().ToLowerInvariant()},\"log\":[{logJson}]}}";
    }

    [Fact]
    public async Task PollsUntilCompleted_DedupsLogsByTimestamp_TicksPerPoll()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{}"); // publishBegin
        handler.Enqueue(HttpStatusCode.OK, EndResponse(false, false, ("2024-01-01T00:00:01", "information", "step1")));
        handler.Enqueue(HttpStatusCode.OK, EndResponse(true, false,
            ("2024-01-01T00:00:01", "information", "step1"),
            ("2024-01-01T00:00:02", "warning", "step2")));

        var logs = new List<Log>();
        var ticks = new List<int>();
        var result = await client.PublishAsync(Request, logs.Add, ticks.Add, pollIntervalMs: 0);

        Assert.True(result.IsCompleted);
        Assert.Equal(
            new[] { "/instance/CustomizationApi/publishBegin", "/instance/CustomizationApi/publishEnd", "/instance/CustomizationApi/publishEnd" },
            handler.Requests.Select(r => r.Request.RequestUri.AbsolutePath));
        Assert.Equal(new[] { "step1", "step2" }, logs.Select(l => l.Message));
        Assert.Equal(new[] { 1, 2 }, ticks);
    }

    [Fact]
    public async Task Failed_ReturnsWithoutThrowing()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{}");
        handler.Enqueue(HttpStatusCode.OK, EndResponse(false, true, ("2024-01-01T00:00:01", "error", "boom")));

        var logs = new List<Log>();
        var result = await client.PublishAsync(Request, logs.Add, null, pollIntervalMs: 0);

        Assert.True(result.IsFailed);
        Assert.Equal("boom", Assert.Single(logs).Message);
    }

    [Fact]
    public async Task Timeout_ThrowsTimeoutException()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{}");
        handler.Enqueue(HttpStatusCode.OK, EndResponse(false, false));

        await Assert.ThrowsAsync<TimeoutException>(
            () => client.PublishAsync(Request, null, null, pollIntervalMs: 0, timeout: TimeSpan.Zero));
    }

    [Fact]
    public async Task Cancellation_IsHonoredBetweenPolls()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{}");
        handler.Enqueue(HttpStatusCode.OK, EndResponse(false, false));

        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.PublishAsync(Request, null, onPollTick: _ => cts.Cancel(),
                pollIntervalMs: 60000, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task BeginRequest_SerializesTenantModeAsString()
    {
        var (client, handler) = CreateClient();
        handler.Enqueue(HttpStatusCode.OK, "{}");
        handler.Enqueue(HttpStatusCode.OK, EndResponse(true, false));

        await client.PublishAsync(Request, null, null, pollIntervalMs: 0);

        Assert.Contains("\"tenantMode\": \"Current\"", handler.Requests[0].Content);
    }
}
