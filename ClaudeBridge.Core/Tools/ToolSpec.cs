namespace ClaudeBridge.Core.Tools;

public sealed record ToolSpec(
    string Name,
    ToolMode Mode,
    string Description,
    IReadOnlyList<ToolParameterSchema> Parameters);
