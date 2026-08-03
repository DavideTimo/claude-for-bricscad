using System.Text.Json;

namespace ClaudeBridge.Core.Json;

/// <summary>
/// Opzioni di (de)serializzazione condivise per input/output dei tool: il Tool Contract usa
/// snake_case (es. "name_pattern", "instance_count") mentre il codice C# usa PascalCase.
/// </summary>
public static class ToolJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };
}
