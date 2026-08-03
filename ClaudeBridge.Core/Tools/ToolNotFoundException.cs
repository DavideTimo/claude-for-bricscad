namespace ClaudeBridge.Core.Tools;

/// <summary>Claude ha richiesto un tool non registrato nel catalogo. Mappa a TOOL_NOT_FOUND.</summary>
public sealed class ToolNotFoundException(string toolName)
    : Exception($"Tool '{toolName}' is not registered.")
{
    public string ToolName { get; } = toolName;
}
