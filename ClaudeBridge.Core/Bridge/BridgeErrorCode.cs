namespace ClaudeBridge.Core.Bridge;

/// <summary>
/// Codici di errore applicativi (semantici) definiti nel Tool Contract, §"Errori applicativi"
/// di ciascun tool. Sollevati dal bridge, mai come eccezione non gestita: l'orchestratore li
/// converte sempre in un risultato tool per Claude.
/// </summary>
public enum BridgeErrorCode
{
    NoDrawingOpen,
    InvalidPattern,
    HandleNotFound,
    NotABlockReference,
    NoAttributes,
    TitleblockNotFound,
    LayoutNotFound,
    LayerNotFound,
    RegionTooLarge,
    EmptySelection,
    InvalidSignature,
    EmptyRegion,
    TagNotFound,
    WriteRejectedByUser,
    LispEvalError,
    BlockedByExecutionPolicy,
}
