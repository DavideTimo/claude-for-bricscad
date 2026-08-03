using System.Text.Json;
using ClaudeBridge.Core.Bridge;
using ClaudeBridge.Core.Json;
using ClaudeBridge.Core.Tools;

namespace ClaudeBridge.Core.Orchestration;

/// <summary>
/// Ciclo di tool-use tra Claude e il bridge BricsCAD: mantiene lo storico conversazione, valida
/// ogni tool call contro il catalogo, applica il gate di conferma per i tool "write", e traduce
/// ogni errore (di schema, applicativo, o interno) in un risultato tool per Claude invece che
/// in un'eccezione che interromperebbe la conversazione (Tool Contract, "Errori comuni").
/// </summary>
public sealed class ClaudeOrchestrator(
    IAnthropicClient client,
    IBricscadBridge bridge,
    Func<ToolUseBlock, CancellationToken, Task<bool>>? confirmWrite = null,
    Action<string>? log = null)
{
    private const int MaxToolIterations = 25;

    private readonly List<ConversationMessage> _history = [];

    public IReadOnlyList<ConversationMessage> History => _history;

    /// <summary>Invia un messaggio utente ed esegue il ciclo di tool-use fino al testo finale di Claude.</summary>
    public async Task<string> SendUserMessageAsync(string userText, CancellationToken ct = default)
    {
        _history.Add(new ConversationMessage(Role.User, [new TextBlock(userText)]));

        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            var turn = await client.SendAsync(_history, ct).ConfigureAwait(false);
            _history.Add(new ConversationMessage(Role.Assistant, turn.Content));

            var toolUses = turn.Content.OfType<ToolUseBlock>().ToList();
            if (toolUses.Count == 0)
            {
                return string.Concat(turn.Content.OfType<TextBlock>().Select(t => t.Text));
            }

            var results = new List<ContentBlock>(toolUses.Count);
            foreach (var toolUse in toolUses)
            {
                results.Add(await ExecuteToolCallAsync(toolUse, ct).ConfigureAwait(false));
            }

            _history.Add(new ConversationMessage(Role.User, results));
        }

        throw new InvalidOperationException($"Exceeded max tool-use iterations ({MaxToolIterations}).");
    }

    /// <summary>Esegue una singola tool call: validazione schema, gate di conferma, dispatch al bridge.</summary>
    public async Task<ToolResultBlock> ExecuteToolCallAsync(ToolUseBlock toolUse, CancellationToken ct = default)
    {
        if (!ToolCatalog.TryGet(toolUse.Name, out var spec))
        {
            return ErrorResult(toolUse.Id, OrchestratorErrorCode.ToolNotFound, $"Tool '{toolUse.Name}' is not registered.");
        }

        try
        {
            ToolSchemaValidator.Validate(spec, toolUse.Input);
        }
        catch (ToolSchemaValidationException ex)
        {
            return ErrorResult(toolUse.Id, OrchestratorErrorCode.SchemaValidationFailed, ex.Message);
        }

        if (spec.Mode == ToolMode.Write)
        {
            var confirmed = confirmWrite is not null && await confirmWrite(toolUse, ct).ConfigureAwait(false);
            if (!confirmed)
            {
                return ErrorResult(toolUse.Id, BridgeErrorCode.WriteRejectedByUser, "User did not confirm this action.");
            }
        }

        try
        {
            var output = await ToolDispatcher.DispatchAsync(bridge, toolUse.Name, toolUse.Input, ct).ConfigureAwait(false);
            var content = JsonSerializer.SerializeToElement(output, output.GetType(), ToolJsonOptions.Default);
            return new ToolResultBlock(toolUse.Id, content, IsError: false);
        }
        catch (BricscadBridgeException ex)
        {
            return ErrorResult(toolUse.Id, ex.Code, ex.Message);
        }
        catch (BridgeUnavailableException ex)
        {
            return ErrorResult(toolUse.Id, OrchestratorErrorCode.BridgeUnavailable, ex.Message);
        }
        catch (ToolSchemaValidationException ex)
        {
            // Deserializzazione dell'input tipizzato fallita dopo la validazione superficiale
            // (es. campo annidato mancante) — trattata comunque come errore di schema.
            return ErrorResult(toolUse.Id, OrchestratorErrorCode.SchemaValidationFailed, ex.Message);
        }
        catch (Exception ex)
        {
            log?.Invoke($"Unhandled exception dispatching tool '{toolUse.Name}': {ex}");
            return ErrorResult(toolUse.Id, OrchestratorErrorCode.InternalError, "Internal error.");
        }
    }

    private static ToolResultBlock ErrorResult(string toolUseId, OrchestratorErrorCode code, string message) =>
        ErrorResult(toolUseId, JsonNamingPolicy.SnakeCaseUpper.ConvertName(code.ToString()), message);

    private static ToolResultBlock ErrorResult(string toolUseId, BridgeErrorCode code, string message) =>
        ErrorResult(toolUseId, JsonNamingPolicy.SnakeCaseUpper.ConvertName(code.ToString()), message);

    private static ToolResultBlock ErrorResult(string toolUseId, string errorCode, string message)
    {
        var payload = JsonSerializer.SerializeToElement(
            new ToolErrorPayload(errorCode, message), ToolJsonOptions.Default);
        return new ToolResultBlock(toolUseId, payload, IsError: true);
    }

    private sealed record ToolErrorPayload(string ErrorCode, string Message);
}
