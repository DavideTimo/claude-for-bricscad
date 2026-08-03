using System.Text.Json;

namespace ClaudeBridge.Core.Tools;

/// <summary>
/// Valida i parametri di primo livello di una tool call contro lo ToolSpec: campi obbligatori
/// presenti, tipo JSON corretto, valori enum ammessi. La validazione *semantica* (es. handle
/// esistente nel disegno) resta responsabilità del bridge (Tool Contract, "Principio di validazione").
/// </summary>
public static class ToolSchemaValidator
{
    public static void Validate(ToolSpec spec, JsonElement input)
    {
        var errors = new List<string>();

        if (input.ValueKind != JsonValueKind.Object)
        {
            throw new ToolSchemaValidationException(spec.Name, ["input must be a JSON object"]);
        }

        foreach (var parameter in spec.Parameters)
        {
            var hasProperty = input.TryGetProperty(parameter.Name, out var value)
                && value.ValueKind != JsonValueKind.Null;

            if (!hasProperty)
            {
                if (parameter.Required)
                {
                    errors.Add($"missing required field '{parameter.Name}'");
                }

                continue;
            }

            if (!parameter.MatchesKind(value.ValueKind))
            {
                errors.Add($"field '{parameter.Name}' expected type {parameter.Type}, got {value.ValueKind}");
                continue;
            }

            if (parameter.AllowedValues is { Count: > 0 } allowed
                && value.ValueKind == JsonValueKind.String
                && !allowed.Contains(value.GetString()))
            {
                errors.Add($"field '{parameter.Name}' must be one of [{string.Join(", ", allowed)}]");
            }
        }

        if (errors.Count > 0)
        {
            throw new ToolSchemaValidationException(spec.Name, errors);
        }
    }
}
