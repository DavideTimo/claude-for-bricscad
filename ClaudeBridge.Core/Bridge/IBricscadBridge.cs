using ClaudeBridge.Core.Bridge.Models;

namespace ClaudeBridge.Core.Bridge;

/// <summary>
/// Contratto astratto verso BricsCAD, come definito nel documento "Tool Contract".
/// Un'implementazione reale (ClaudeBridge.BricscadPlugin) deve marshalling ogni chiamata
/// sul thread principale dell'applicazione: questa interfaccia non lo presuppone, lo richiede
/// come vincolo implementativo (vedi Tool Contract, sezione "Thread-marshaling").
/// </summary>
public interface IBricscadBridge
{
    // Livello 0 — tag persistenti (XDATA)
    Task<FindTaggedEntitiesOutput> FindTaggedEntitiesAsync(FindTaggedEntitiesInput input, CancellationToken ct = default);
    Task<TagEntitiesAsCategoryOutput> TagEntitiesAsCategoryAsync(TagEntitiesAsCategoryInput input, CancellationToken ct = default);

    // Livello 1 — nomi/attributi
    Task<ListBlockDefinitionsOutput> ListBlockDefinitionsAsync(ListBlockDefinitionsInput input, CancellationToken ct = default);
    Task<FindBlocksOutput> FindBlocksAsync(FindBlocksInput input, CancellationToken ct = default);
    Task<GetBlockAttributesOutput> GetBlockAttributesAsync(GetBlockAttributesInput input, CancellationToken ct = default);
    Task<SetBlockAttributeOutput> SetBlockAttributeAsync(SetBlockAttributeInput input, CancellationToken ct = default);
    Task<GetTitleblockDataOutput> GetTitleblockDataAsync(GetTitleblockDataInput input, CancellationToken ct = default);
    Task<ListLayersOutput> ListLayersAsync(ListLayersInput input, CancellationToken ct = default);
    Task<GetEntitiesByLayerOutput> GetEntitiesByLayerAsync(GetEntitiesByLayerInput input, CancellationToken ct = default);

    // Livello 2 — matching geometrico
    Task<ListEntityClustersOutput> ListEntityClustersAsync(ListEntityClustersInput input, CancellationToken ct = default);
    Task<GetGeometrySignatureOutput> GetGeometrySignatureAsync(GetGeometrySignatureInput input, CancellationToken ct = default);
    Task<FindSimilarGeometryOutput> FindSimilarGeometryAsync(FindSimilarGeometryInput input, CancellationToken ct = default);
    Task<SaveSignatureAsCategoryOutput> SaveSignatureAsCategoryAsync(SaveSignatureAsCategoryInput input, CancellationToken ct = default);
    Task<ListLearnedCategoriesOutput> ListLearnedCategoriesAsync(ListLearnedCategoriesInput input, CancellationToken ct = default);

    // Livello 3 — conferma visiva mirata
    Task<GetTextInRegionOutput> GetTextInRegionAsync(GetTextInRegionInput input, CancellationToken ct = default);
    Task<GetRegionScreenshotOutput> GetRegionScreenshotAsync(GetRegionScreenshotInput input, CancellationToken ct = default);
    Task<ExportCurrentViewImageOutput> ExportCurrentViewImageAsync(ExportCurrentViewImageInput input, CancellationToken ct = default);

    // Azioni ed esecuzione
    Task<HighlightEntitiesOutput> HighlightEntitiesAsync(HighlightEntitiesInput input, CancellationToken ct = default);
    Task<ZoomToOutput> ZoomToAsync(ZoomToInput input, CancellationToken ct = default);
    Task<RunLispOutput> RunLispAsync(RunLispInput input, CancellationToken ct = default);
}
