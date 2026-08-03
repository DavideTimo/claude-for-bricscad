namespace ClaudeBridge.Core.Tools;

/// <summary>
/// read: eseguibile senza conferma. write: richiede sempre conferma UI esplicita
/// prima che il bridge venga invocato (Tool Contract, "Principio di validazione").
/// </summary>
public enum ToolMode
{
    Read,
    Write,
}
