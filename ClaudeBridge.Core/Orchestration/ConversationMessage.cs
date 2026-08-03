namespace ClaudeBridge.Core.Orchestration;

public enum Role
{
    User,
    Assistant,
}

public sealed record ConversationMessage(Role Role, IReadOnlyList<ContentBlock> Content);

/// <summary>Turno restituito da Claude: blocchi di contenuto (testo e/o tool_use) più il motivo di stop.</summary>
public sealed record AssistantTurn(IReadOnlyList<ContentBlock> Content, string StopReason)
{
    public bool RequestsToolUse => Content.OfType<ToolUseBlock>().Any();
}
