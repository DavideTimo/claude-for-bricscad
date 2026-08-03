namespace ClaudeBridge.Core.Bridge;

/// <summary>
/// Errore applicativo restituito dal bridge (es. handle non trovato, disegno non aperto).
/// L'orchestratore la intercetta e la traduce in un risultato tool per Claude, non la propaga.
/// </summary>
public sealed class BricscadBridgeException(BridgeErrorCode code, string? message = null)
    : Exception(message ?? code.ToString())
{
    public BridgeErrorCode Code { get; } = code;
}

/// <summary>
/// Segnala che il bridge non è raggiungibile (BricsCAD chiuso o plugin non caricato),
/// distinto da un errore applicativo: mappa a BRIDGE_UNAVAILABLE lato orchestratore.
/// </summary>
public sealed class BridgeUnavailableException() : Exception("BricsCAD bridge is not reachable.");
