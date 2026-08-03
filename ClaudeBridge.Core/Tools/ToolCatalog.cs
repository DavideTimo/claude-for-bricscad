using System.Text.Json.Nodes;

namespace ClaudeBridge.Core.Tools;

/// <summary>
/// Registro di tutti i tool definiti nel documento "Tool Contract": nome, mode e schema dei
/// parametri di primo livello, usato dall'orchestratore per la validazione (§"Principio di
/// validazione") e per generare lo schema JSON da passare a Claude nella richiesta Messages API.
/// </summary>
public static class ToolCatalog
{
    public static IReadOnlyList<ToolSpec> All { get; } = BuildAll();

    private static readonly Dictionary<string, ToolSpec> ByName =
        All.ToDictionary(t => t.Name, t => t);

    public static bool TryGet(string name, out ToolSpec spec) => ByName.TryGetValue(name, out spec!);

    /// <summary>Proietta lo schema dei parametri nel formato "input_schema" atteso dalla Messages API.</summary>
    public static JsonObject BuildInputSchema(ToolSpec spec)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var p in spec.Parameters)
        {
            var property = new JsonObject { ["type"] = JsonSchemaType(p.Type) };
            if (p.AllowedValues is { Count: > 0 })
            {
                property["enum"] = new JsonArray(p.AllowedValues.Select(v => (JsonNode)v).ToArray());
            }

            properties[p.Name] = property;
            if (p.Required)
            {
                required.Add(p.Name);
            }
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
        };
    }

    private static string JsonSchemaType(ToolParameterType type) => type switch
    {
        ToolParameterType.String => "string",
        ToolParameterType.Number => "number",
        ToolParameterType.Boolean => "boolean",
        ToolParameterType.Array => "array",
        ToolParameterType.Object => "object",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static List<ToolSpec> BuildAll()
    {
        static ToolParameterSchema Str(string name, bool required = false, IReadOnlyList<string>? allowed = null) =>
            new(name, ToolParameterType.String, required, allowed);

        static ToolParameterSchema Num(string name, bool required = false) =>
            new(name, ToolParameterType.Number, required);

        static ToolParameterSchema Bool(string name, bool required = false) =>
            new(name, ToolParameterType.Boolean, required);

        static ToolParameterSchema Arr(string name, bool required = false) =>
            new(name, ToolParameterType.Array, required);

        static ToolParameterSchema Obj(string name, bool required = false) =>
            new(name, ToolParameterType.Object, required);

        var spaceEnum = new[] { "model", "paper", "any" };

        return
        [
            new ToolSpec("find_tagged_entities", ToolMode.Read,
                "Cerca entità già taggate in una sessione precedente su questo disegno (Livello 0).",
                [Str("label")]),

            new ToolSpec("tag_entities_as_category", ToolMode.Write,
                "Persiste un'etichetta direttamente nel file (XDATA), per riconoscimento immediato in sessioni future.",
                [Arr("handles", required: true), Str("label", required: true)]),

            new ToolSpec("list_block_definitions", ToolMode.Read,
                "Scopre tutti i blocchi presenti nel disegno corrente (tool di scoperta, Livello 1).",
                [Str("space", allowed: spaceEnum)]),

            new ToolSpec("find_blocks", ToolMode.Read,
                "Trova istanze di blocco per nome (con wildcard) ed eventuale layer.",
                [Str("name_pattern", required: true), Str("layer"), Str("space", allowed: spaceEnum)]),

            new ToolSpec("get_block_attributes", ToolMode.Read,
                "Legge tutti gli attributi (tag/valore) di una specifica istanza di blocco.",
                [Str("handle", required: true)]),

            new ToolSpec("set_block_attribute", ToolMode.Write,
                "Scrive un attributo di un blocco. Richiede conferma UI esplicita.",
                [Str("handle", required: true), Str("tag", required: true), Str("value", required: true)]),

            new ToolSpec("get_titleblock_data", ToolMode.Read,
                "Shortcut dedicato al cartiglio: campi e validazione di completezza.",
                [Str("layout")]),

            new ToolSpec("list_layers", ToolMode.Read,
                "Elenco layer del disegno.",
                []),

            new ToolSpec("get_entities_by_layer", ToolMode.Read,
                "Entità per layer, con filtro opzionale per tipo.",
                [Str("layer", required: true), Str("entity_type")]),

            new ToolSpec("list_entity_clusters", ToolMode.Read,
                "Segmenta il disegno (o un'area) in gruppi di entità vicine, candidati 'simbolo'.",
                [Obj("bounding_box", required: true), Str("layout"), Num("max_cluster_extent")]),

            new ToolSpec("get_geometry_signature", ToolMode.Read,
                "Estrae la firma geometrica invariante a scala/rotazione di un gruppo di entità.",
                [Arr("handles", required: true)]),

            new ToolSpec("find_similar_geometry", ToolMode.Read,
                "Cerca nel disegno cluster con firma simile a quella data, con punteggio di similarità.",
                [Obj("signature", required: true), Num("tolerance"), Str("space", allowed: spaceEnum), Obj("bounding_box")]),

            new ToolSpec("save_signature_as_category", ToolMode.Read,
                "Salva una firma come categoria riconosciuta (sessione o libreria personale).",
                [Obj("signature", required: true), Str("label", required: true), Str("scope", required: true, allowed: ["session", "personal_library"])]),

            new ToolSpec("list_learned_categories", ToolMode.Read,
                "Elenca le categorie già salvate nella libreria personale.",
                []),

            new ToolSpec("get_text_in_region", ToolMode.Read,
                "Legge testo grezzo (TEXT/MTEXT) presente in un'area nota del disegno.",
                [Obj("bounding_box", required: true), Str("layout")]),

            new ToolSpec("get_region_screenshot", ToolMode.Read,
                "Screenshot raster di una zona specifica e delimitata, per conferma visiva mirata.",
                [Obj("bounding_box", required: true), Str("layout"), Num("zoom_margin_factor"), Num("max_resolution")]),

            new ToolSpec("export_current_view_image", ToolMode.Read,
                "Screenshot dell'intera vista corrente, da usare solo come complemento.",
                [Str("layout"), Num("max_resolution")]),

            new ToolSpec("highlight_entities", ToolMode.Write,
                "Evidenzia entità nel disegno (temporaneo, non salvato nel DWG).",
                [Arr("handles", required: true), Str("color"), Bool("clear_previous")]),

            new ToolSpec("zoom_to", ToolMode.Read,
                "Centra la vista su un'entità.",
                [Str("handle", required: true), Num("margin_factor")]),

            new ToolSpec("run_lisp", ToolMode.Write,
                "Esecuzione di un'espressione LISP generica, solo per azioni non coperte da tool dedicati.",
                [Str("expression", required: true), Str("reason", required: true)]),
        ];
    }
}
