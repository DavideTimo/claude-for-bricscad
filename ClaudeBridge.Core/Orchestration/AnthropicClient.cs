using System.Text;

namespace ClaudeBridge.Core.Orchestration;

/// <summary>
/// Implementazione reale di IAnthropicClient verso la Messages API di Anthropic (Fase 1 della
/// roadmap). Riceve un HttpClient già configurato dal chiamante (permette di iniettare timeout,
/// proxy, o — nei test — un HttpMessageHandler fittizio senza toccare la rete).
/// </summary>
public sealed class AnthropicClient(
    HttpClient httpClient,
    string apiKey,
    string model,
    int maxTokens = 4096,
    string? systemPrompt = null,
    Uri? endpoint = null) : IAnthropicClient
{
    private const string AnthropicVersion = "2023-06-01";
    private static readonly Uri DefaultEndpoint = new("https://api.anthropic.com/v1/messages");

    private readonly Uri _endpoint = endpoint ?? DefaultEndpoint;

    public async Task<AssistantTurn> SendAsync(IReadOnlyList<ConversationMessage> history, CancellationToken ct = default)
    {
        var requestBody = AnthropicWireFormat.BuildRequestBody(model, maxTokens, systemPrompt, history);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);

        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw AnthropicApiException.FromResponse(response.StatusCode, responseBody);
        }

        return AnthropicWireFormat.ParseAssistantTurn(responseBody);
    }
}
