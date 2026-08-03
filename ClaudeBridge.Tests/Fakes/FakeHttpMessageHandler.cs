using System.Net;

namespace ClaudeBridge.Tests.Fakes;

/// <summary>
/// HttpMessageHandler fittizio: intercetta ogni richiesta senza toccare la rete, registra il
/// corpo inviato e restituisce una risposta scriptata. Usato per testare AnthropicClient.
/// </summary>
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    public static FakeHttpMessageHandler ReturningJson(HttpStatusCode statusCode, string json) =>
        new((_, _) => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
        Requests.Add((request, body));
        return respond(request, body);
    }
}
