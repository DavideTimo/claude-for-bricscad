using ClaudeBridge.Core.Orchestration;

namespace ClaudeBridge.Tests.Fakes;

/// <summary>
/// Client Anthropic fittizio che restituisce una sequenza predefinita di turni, uno per ogni
/// chiamata a SendAsync. Usato per guidare deterministicamente il ciclo di tool-use nei test.
/// </summary>
public sealed class ScriptedAnthropicClient(params AssistantTurn[] turns) : IAnthropicClient
{
    private int _index;
    public List<IReadOnlyList<ConversationMessage>> ReceivedHistories { get; } = [];

    public Task<AssistantTurn> SendAsync(IReadOnlyList<ConversationMessage> history, CancellationToken ct = default)
    {
        ReceivedHistories.Add(history);
        if (_index >= turns.Length)
        {
            throw new InvalidOperationException("ScriptedAnthropicClient: no more scripted turns.");
        }

        return Task.FromResult(turns[_index++]);
    }
}
