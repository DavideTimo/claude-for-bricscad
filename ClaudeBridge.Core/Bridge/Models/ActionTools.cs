namespace ClaudeBridge.Core.Bridge.Models;

public sealed record HighlightEntitiesInput(
    IReadOnlyList<string> Handles,
    string? Color = null,
    bool ClearPrevious = true);

public sealed record HighlightEntitiesOutput(int HighlightedCount, IReadOnlyList<string> NotFoundHandles);

public sealed record ZoomToInput(string Handle, double MarginFactor = 1.5);

public sealed record ZoomToOutput(bool Success);

public sealed record RunLispInput(string Expression, string Reason);

public sealed record RunLispOutput(bool Success, string? Result, string? ErrorMessage, bool BlockedByPolicy);
