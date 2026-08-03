using System.Text.Json;

namespace ClaudeBridge.Core.Tools;

public enum ToolParameterType
{
    String,
    Number,
    Boolean,
    Array,
    Object,
}

public sealed record ToolParameterSchema(
    string Name,
    ToolParameterType Type,
    bool Required,
    IReadOnlyList<string>? AllowedValues = null)
{
    public bool MatchesKind(JsonValueKind kind) => Type switch
    {
        ToolParameterType.String => kind == JsonValueKind.String,
        ToolParameterType.Number => kind is JsonValueKind.Number,
        ToolParameterType.Boolean => kind is JsonValueKind.True or JsonValueKind.False,
        ToolParameterType.Array => kind == JsonValueKind.Array,
        ToolParameterType.Object => kind == JsonValueKind.Object,
        _ => false,
    };
}
