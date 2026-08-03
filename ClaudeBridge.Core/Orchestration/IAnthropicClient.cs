namespace ClaudeBridge.Core.Orchestration;

/// <summary>
/// Astrazione verso l'API Anthropic (Messages API), su cui è costruito il ciclo di tool-use.
/// L'implementazione HTTP concreta (AnthropicClient) è prevista in Fase 1 della roadmap; per ora
/// questa interfaccia permette di testare ClaudeOrchestrator con un client fittizio.
/// </summary>
public interface IAnthropicClient
{
    Task<AssistantTurn> SendAsync(IReadOnlyList<ConversationMessage> history, CancellationToken ct = default);
}
