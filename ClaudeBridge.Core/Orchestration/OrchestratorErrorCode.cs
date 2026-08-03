namespace ClaudeBridge.Core.Orchestration;

/// <summary>Errori di livello orchestratore, validi per tutti i tool (Tool Contract, "Errori comuni").</summary>
public enum OrchestratorErrorCode
{
    SchemaValidationFailed,
    ToolNotFound,
    BridgeUnavailable,
    InternalError,
}
