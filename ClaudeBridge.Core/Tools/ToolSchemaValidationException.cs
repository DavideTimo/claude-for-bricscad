namespace ClaudeBridge.Core.Tools;

/// <summary>
/// Parametri di un tool non conformi allo schema (tipo, campo obbligatorio, enum).
/// Sollevata dall'orchestratore prima di invocare il bridge. Mappa a SCHEMA_VALIDATION_FAILED.
/// </summary>
public sealed class ToolSchemaValidationException(string toolName, IReadOnlyList<string> errors)
    : Exception($"Tool '{toolName}' input failed schema validation: {string.Join("; ", errors)}")
{
    public string ToolName { get; } = toolName;
    public IReadOnlyList<string> Errors { get; } = errors;
}
