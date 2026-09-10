using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AcuPackageTools.Core.UnitTests;

/// <summary>
/// Scripted HttpMessageHandler: responses are dequeued in order; every request
/// (and its content, read before the response is produced) is recorded.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

    public List<(HttpRequestMessage Request, string Content)> Requests { get; } = new();

    public void Enqueue(HttpStatusCode status, string content = "")
        => _responders.Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(content ?? string.Empty, Encoding.UTF8, "application/json")
        });

    public void EnqueueRepeating(HttpStatusCode status, string content, int count)
    {
        for (int i = 0; i < count; i++) Enqueue(status, content);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string content = request.Content == null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, content));
        if (_responders.Count == 0)
            throw new InvalidOperationException("No scripted response for " + request.RequestUri);
        return _responders.Dequeue()(request);
    }
}
