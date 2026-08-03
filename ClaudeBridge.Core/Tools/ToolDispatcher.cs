using System.Text.Json;
using ClaudeBridge.Core.Bridge;
using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.Core.Json;

namespace ClaudeBridge.Core.Tools;

/// <summary>
/// Deserializza l'input JSON di una tool call nel record tipizzato del tool, invoca il metodo
/// corrispondente su IBricscadBridge e restituisce l'output tipizzato (poi serializzato dal
/// chiamante). Presuppone che ToolSchemaValidator.Validate sia già stato eseguito con successo.
/// </summary>
public static class ToolDispatcher
{
    public static async Task<object> DispatchAsync(
        IBricscadBridge bridge,
        string toolName,
        JsonElement input,
        CancellationToken ct = default)
    {
        return toolName switch
        {
            "find_tagged_entities" => await bridge.FindTaggedEntitiesAsync(Deserialize<FindTaggedEntitiesInput>(toolName, input), ct),
            "tag_entities_as_category" => await bridge.TagEntitiesAsCategoryAsync(Deserialize<TagEntitiesAsCategoryInput>(toolName, input), ct),

            "list_block_definitions" => await bridge.ListBlockDefinitionsAsync(Deserialize<ListBlockDefinitionsInput>(toolName, input), ct),
            "find_blocks" => await bridge.FindBlocksAsync(Deserialize<FindBlocksInput>(toolName, input), ct),
            "get_block_attributes" => await bridge.GetBlockAttributesAsync(Deserialize<GetBlockAttributesInput>(toolName, input), ct),
            "set_block_attribute" => await bridge.SetBlockAttributeAsync(Deserialize<SetBlockAttributeInput>(toolName, input), ct),
            "get_titleblock_data" => await bridge.GetTitleblockDataAsync(Deserialize<GetTitleblockDataInput>(toolName, input), ct),
            "list_layers" => await bridge.ListLayersAsync(Deserialize<ListLayersInput>(toolName, input), ct),
            "get_entities_by_layer" => await bridge.GetEntitiesByLayerAsync(Deserialize<GetEntitiesByLayerInput>(toolName, input), ct),

            "list_entity_clusters" => await bridge.ListEntityClustersAsync(Deserialize<ListEntityClustersInput>(toolName, input), ct),
            "get_geometry_signature" => await bridge.GetGeometrySignatureAsync(Deserialize<GetGeometrySignatureInput>(toolName, input), ct),
            "find_similar_geometry" => await bridge.FindSimilarGeometryAsync(Deserialize<FindSimilarGeometryInput>(toolName, input), ct),
            "save_signature_as_category" => await bridge.SaveSignatureAsCategoryAsync(Deserialize<SaveSignatureAsCategoryInput>(toolName, input), ct),
            "list_learned_categories" => await bridge.ListLearnedCategoriesAsync(Deserialize<ListLearnedCategoriesInput>(toolName, input), ct),

            "get_text_in_region" => await bridge.GetTextInRegionAsync(Deserialize<GetTextInRegionInput>(toolName, input), ct),
            "get_region_screenshot" => await bridge.GetRegionScreenshotAsync(Deserialize<GetRegionScreenshotInput>(toolName, input), ct),
            "export_current_view_image" => await bridge.ExportCurrentViewImageAsync(Deserialize<ExportCurrentViewImageInput>(toolName, input), ct),

            "highlight_entities" => await bridge.HighlightEntitiesAsync(Deserialize<HighlightEntitiesInput>(toolName, input), ct),
            "zoom_to" => await bridge.ZoomToAsync(Deserialize<ZoomToInput>(toolName, input), ct),
            "run_lisp" => await bridge.RunLispAsync(Deserialize<RunLispInput>(toolName, input), ct),

            _ => throw new ToolNotFoundException(toolName),
        };
    }

    private static T Deserialize<T>(string toolName, JsonElement input)
    {
        try
        {
            return input.Deserialize<T>(ToolJsonOptions.Default)
                ?? throw new ToolSchemaValidationException(toolName, ["input deserialized to null"]);
        }
        catch (JsonException ex)
        {
            throw new ToolSchemaValidationException(toolName, [ex.Message]);
        }
    }
}
