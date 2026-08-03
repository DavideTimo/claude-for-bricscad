namespace ClaudeBridge.Core.Bridge;

/// <summary>
/// Whitelist/blacklist per run_lisp (Tool Contract §9, Sicurezza &amp; Privacy §5bis): rifiuta
/// espressioni pericolose *prima* che arrivino alla conferma UI, non si affida alla sola
/// conferma utente come barriera. Vive in Core (non nel bridge BricsCAD specifico) perché sia il
/// bridge simulato sia una futura implementazione reale devono applicare la stessa policy, senza
/// divergere.
/// </summary>
public static class LispExecutionPolicy
{
    /// <summary>
    /// Funzioni AutoLISP/Visual LISP che accedono al filesystem fuori dall'ambito del disegno,
    /// eseguono comandi di sistema/shell, o modificano configurazioni globali non legate al
    /// disegno corrente (Tool Contract §9). Elenco di partenza da ampliare solo se un caso reale
    /// lo richiede, non da restringere dopo un incidente.
    /// </summary>
    private static readonly string[] BlacklistedFunctions =
    [
        "open", "close", "write-line", "write-char", "read-line", "read-char",
        "vl-file-delete", "vl-file-copy", "vl-file-rename", "vl-file-directory-p",
        "vl-directory-files", "vl-mkdir",
        "startapp", "shell", "dos_", "vl-registry-write", "vl-registry-delete",
    ];

    /// <summary>
    /// Restituisce true se l'espressione è ammessa. Se false, <paramref name="reason"/> spiega
    /// quale funzione vietata l'ha bloccata, in un linguaggio mostrabile all'utente.
    /// </summary>
    public static bool IsAllowed(string expression, out string? reason)
    {
        foreach (var function in BlacklistedFunctions)
        {
            if (ContainsFunctionCall(expression, function))
            {
                reason = $"L'espressione usa '{function}', vietata dalla execution policy (accesso filesystem/sistema o configurazione globale).";
                return false;
            }
        }

        reason = null;
        return true;
    }

    private static bool ContainsFunctionCall(string expression, string function)
    {
        // Match approssimato ma deliberatamente prudente: cerca il nome funzione come token
        // isolato (delimitato da '(', spazio o fine stringa su entrambi i lati), non come
        // sottostringa di un identificatore più lungo (es. "openings-count" non è "open").
        var index = 0;
        while ((index = expression.IndexOf(function, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var precededSafely = index == 0 || IsDelimiter(expression[index - 1]);
            var followingIndex = index + function.Length;
            var followedSafely = followingIndex == expression.Length || IsDelimiter(expression[followingIndex]);

            if (precededSafely && followedSafely)
            {
                return true;
            }

            index += function.Length;
        }

        return false;
    }

    private static bool IsDelimiter(char c) => c is '(' or ')' or ' ' or '\t' or '\n' or '\r' or '"';
}
