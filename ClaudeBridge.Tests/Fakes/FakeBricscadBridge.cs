using ClaudeBridge.Core.Bridge;
using ClaudeBridge.Core.Bridge.Models;

namespace ClaudeBridge.Tests.Fakes;

/// <summary>
/// Bridge fittizio in memoria, senza alcuna dipendenza da BricsCAD: usato per testare
/// l'orchestratore e il dispatcher (Piano di Test §1, "non richiedono BricsCAD aperto").
/// Ogni metodo può essere sovrascritto assegnando i relativi delegate prima della chiamata.
/// </summary>
public sealed class FakeBricscadBridge : IBricscadBridge
{
    public Func<FindTaggedEntitiesInput, Task<FindTaggedEntitiesOutput>>? OnFindTaggedEntities { get; set; }
    public Func<TagEntitiesAsCategoryInput, Task<TagEntitiesAsCategoryOutput>>? OnTagEntitiesAsCategory { get; set; }
    public Func<ListBlockDefinitionsInput, Task<ListBlockDefinitionsOutput>>? OnListBlockDefinitions { get; set; }
    public Func<FindBlocksInput, Task<FindBlocksOutput>>? OnFindBlocks { get; set; }
    public Func<GetBlockAttributesInput, Task<GetBlockAttributesOutput>>? OnGetBlockAttributes { get; set; }
    public Func<SetBlockAttributeInput, Task<SetBlockAttributeOutput>>? OnSetBlockAttribute { get; set; }
    public Func<GetTitleblockDataInput, Task<GetTitleblockDataOutput>>? OnGetTitleblockData { get; set; }
    public Func<ListLayersInput, Task<ListLayersOutput>>? OnListLayers { get; set; }
    public Func<GetEntitiesByLayerInput, Task<GetEntitiesByLayerOutput>>? OnGetEntitiesByLayer { get; set; }
    public Func<ListEntityClustersInput, Task<ListEntityClustersOutput>>? OnListEntityClusters { get; set; }
    public Func<GetGeometrySignatureInput, Task<GetGeometrySignatureOutput>>? OnGetGeometrySignature { get; set; }
    public Func<FindSimilarGeometryInput, Task<FindSimilarGeometryOutput>>? OnFindSimilarGeometry { get; set; }
    public Func<SaveSignatureAsCategoryInput, Task<SaveSignatureAsCategoryOutput>>? OnSaveSignatureAsCategory { get; set; }
    public Func<ListLearnedCategoriesInput, Task<ListLearnedCategoriesOutput>>? OnListLearnedCategories { get; set; }
    public Func<GetTextInRegionInput, Task<GetTextInRegionOutput>>? OnGetTextInRegion { get; set; }
    public Func<GetRegionScreenshotInput, Task<GetRegionScreenshotOutput>>? OnGetRegionScreenshot { get; set; }
    public Func<ExportCurrentViewImageInput, Task<ExportCurrentViewImageOutput>>? OnExportCurrentViewImage { get; set; }
    public Func<HighlightEntitiesInput, Task<HighlightEntitiesOutput>>? OnHighlightEntities { get; set; }
    public Func<ZoomToInput, Task<ZoomToOutput>>? OnZoomTo { get; set; }
    public Func<RunLispInput, Task<RunLispOutput>>? OnRunLisp { get; set; }

    public int CallCount { get; private set; }

    public Task<FindTaggedEntitiesOutput> FindTaggedEntitiesAsync(FindTaggedEntitiesInput input, CancellationToken ct = default) =>
        Invoke(OnFindTaggedEntities, input);

    public Task<TagEntitiesAsCategoryOutput> TagEntitiesAsCategoryAsync(TagEntitiesAsCategoryInput input, CancellationToken ct = default) =>
        Invoke(OnTagEntitiesAsCategory, input);

    public Task<ListBlockDefinitionsOutput> ListBlockDefinitionsAsync(ListBlockDefinitionsInput input, CancellationToken ct = default) =>
        Invoke(OnListBlockDefinitions, input);

    public Task<FindBlocksOutput> FindBlocksAsync(FindBlocksInput input, CancellationToken ct = default) =>
        Invoke(OnFindBlocks, input);

    public Task<GetBlockAttributesOutput> GetBlockAttributesAsync(GetBlockAttributesInput input, CancellationToken ct = default) =>
        Invoke(OnGetBlockAttributes, input);

    public Task<SetBlockAttributeOutput> SetBlockAttributeAsync(SetBlockAttributeInput input, CancellationToken ct = default) =>
        Invoke(OnSetBlockAttribute, input);

    public Task<GetTitleblockDataOutput> GetTitleblockDataAsync(GetTitleblockDataInput input, CancellationToken ct = default) =>
        Invoke(OnGetTitleblockData, input);

    public Task<ListLayersOutput> ListLayersAsync(ListLayersInput input, CancellationToken ct = default) =>
        Invoke(OnListLayers, input);

    public Task<GetEntitiesByLayerOutput> GetEntitiesByLayerAsync(GetEntitiesByLayerInput input, CancellationToken ct = default) =>
        Invoke(OnGetEntitiesByLayer, input);

    public Task<ListEntityClustersOutput> ListEntityClustersAsync(ListEntityClustersInput input, CancellationToken ct = default) =>
        Invoke(OnListEntityClusters, input);

    public Task<GetGeometrySignatureOutput> GetGeometrySignatureAsync(GetGeometrySignatureInput input, CancellationToken ct = default) =>
        Invoke(OnGetGeometrySignature, input);

    public Task<FindSimilarGeometryOutput> FindSimilarGeometryAsync(FindSimilarGeometryInput input, CancellationToken ct = default) =>
        Invoke(OnFindSimilarGeometry, input);

    public Task<SaveSignatureAsCategoryOutput> SaveSignatureAsCategoryAsync(SaveSignatureAsCategoryInput input, CancellationToken ct = default) =>
        Invoke(OnSaveSignatureAsCategory, input);

    public Task<ListLearnedCategoriesOutput> ListLearnedCategoriesAsync(ListLearnedCategoriesInput input, CancellationToken ct = default) =>
        Invoke(OnListLearnedCategories, input);

    public Task<GetTextInRegionOutput> GetTextInRegionAsync(GetTextInRegionInput input, CancellationToken ct = default) =>
        Invoke(OnGetTextInRegion, input);

    public Task<GetRegionScreenshotOutput> GetRegionScreenshotAsync(GetRegionScreenshotInput input, CancellationToken ct = default) =>
        Invoke(OnGetRegionScreenshot, input);

    public Task<ExportCurrentViewImageOutput> ExportCurrentViewImageAsync(ExportCurrentViewImageInput input, CancellationToken ct = default) =>
        Invoke(OnExportCurrentViewImage, input);

    public Task<HighlightEntitiesOutput> HighlightEntitiesAsync(HighlightEntitiesInput input, CancellationToken ct = default) =>
        Invoke(OnHighlightEntities, input);

    public Task<ZoomToOutput> ZoomToAsync(ZoomToInput input, CancellationToken ct = default) =>
        Invoke(OnZoomTo, input);

    public Task<RunLispOutput> RunLispAsync(RunLispInput input, CancellationToken ct = default) =>
        Invoke(OnRunLisp, input);

    private Task<TOut> Invoke<TIn, TOut>(Func<TIn, Task<TOut>>? handler, TIn input)
    {
        CallCount++;
        return handler is null
            ? throw new InvalidOperationException($"FakeBricscadBridge: no handler configured for {typeof(TIn).Name}.")
            : handler(input);
    }
}
