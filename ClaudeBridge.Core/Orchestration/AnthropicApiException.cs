using System.Net;
using System.Text.Json;

namespace ClaudeBridge.Core.Orchestration;

/// <summary>
/// Errore HTTP/applicativo restituito dall'API Anthropic (rate limit, chiave non valida, ecc.).
/// Un'eccezione tipizzata e catturabile: chi guida la sessione di chat (orchestratore o, più a
/// monte, la UI) può intercettarla e mostrare un messaggio comprensibile invece di far cadere
/// l'intera sessione (Piano di Test §1, "Client API Anthropic").
/// </summary>
public sealed class AnthropicApiException : Exception
{
    private AnthropicApiException(HttpStatusCode statusCode, string? errorType, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>Tipo di errore Anthropic (es. "rate_limit_error", "authentication_error"), se presente nel body.</summary>
    public string? ErrorType { get; }

    public static AnthropicApiException FromResponse(HttpStatusCode statusCode, string responseBody)
    {
        string? errorType = null;
        var message = $"Anthropic API request failed with status {(int)statusCode} {statusCode}.";

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("type", out var typeElement))
                {
                    errorType = typeElement.GetString();
                }

                if (error.TryGetProperty("message", out var messageElement) && messageElement.GetString() is { } m)
                {
                    message = m;
                }
            }
        }
        catch (JsonException)
        {
            // Corpo non-JSON (es. errore del gateway/proxy prima di raggiungere l'API): si usa il messaggio generico.
        }

        return new AnthropicApiException(statusCode, errorType, message);
    }
}
