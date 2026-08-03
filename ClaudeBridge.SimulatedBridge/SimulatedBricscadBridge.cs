using ClaudeBridge.Core.Bridge;
using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.GeometryEngine;

namespace ClaudeBridge.SimulatedBridge;

/// <summary>
/// Implementazione di IBricscadBridge su un disegno in memoria (SimulatedDrawing), appoggiata a
/// ClaudeBridge.GeometryEngine per clustering/firme/matching. Non sostituisce il vero plugin
/// BricsCAD (§"Note e limiti" più sotto), ma permette di far girare l'intero ciclo di tool-use
/// senza BricsCAD — per demo e test di integrazione (Piano di Test, "senza BricsCAD aperto").
///
/// Note e limiti noti: `space` (model/paper) non è modellato, GetEntitiesByLayer copre solo i
/// blocchi (non la geometria sciolta o il testo), e gli screenshot sono un placeholder statico,
/// non un vero render — semplificazioni accettabili per un ambiente simulato, non per un plugin
/// reale.
/// </summary>
public sealed class SimulatedBricscadBridge(
    SimulatedDrawing drawing,
    SymbolLibrary symbolLibrary,
    double clusteringGapTolerance = 0.5) : IBricscadBridge
{
    // PNG 1x1 trasparente: segnaposto esplicito, non un vero render (nessun motore grafico qui).
    private const string PlaceholderPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    public Task<FindTaggedEntitiesOutput> FindTaggedEntitiesAsync(FindTaggedEntitiesInput input, CancellationToken ct = default)
    {
        var matches = drawing.Tags.Values
            .Where(t => input.Label is null || t.Label == input.Label)
            .ToList();
        return Task.FromResult(new FindTaggedEntitiesOutput(matches));
    }

    public Task<TagEntitiesAsCategoryOutput> TagEntitiesAsCategoryAsync(TagEntitiesAsCategoryInput input, CancellationToken ct = default)
    {
        EnsureHandlesExist(input.Handles);
        var now = DateTimeOffset.UtcNow;
        foreach (var handle in input.Handles)
        {
            drawing.Tag(handle, input.Label, now);
        }

        return Task.FromResult(new TagEntitiesAsCategoryOutput(input.Handles.Count, input.Label));
    }

    public Task<ListBlockDefinitionsOutput> ListBlockDefinitionsAsync(ListBlockDefinitionsInput input, CancellationToken ct = default)
    {
        var definitions = drawing.Blocks
            .GroupBy(b => b.BlockName)
            .Select(g => new BlockDefinition(
                g.Key,
                g.Count(),
                g.SelectMany(b => b.Attributes.Select(a => a.Tag)).Distinct().ToList(),
                g.First().Layer))
            .ToList();
        return Task.FromResult(new ListBlockDefinitionsOutput(definitions));
    }

    public Task<FindBlocksOutput> FindBlocksAsync(FindBlocksInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(input.NamePattern))
        {
            throw new BricscadBridgeException(BridgeErrorCode.InvalidPattern, "name_pattern non può essere vuoto.");
        }

        var matches = drawing.Blocks
            .Where(b => WildcardMatcher.IsMatch(input.NamePattern, b.BlockName))
            .Where(b => input.Layer is null || b.Layer == input.Layer)
            .Select(b => new BlockMatch(b.Handle, b.BlockName, b.Layer, b.InsertionPoint, b.Layout, b.Attributes.Count > 0))
            .ToList();

        return Task.FromResult(new FindBlocksOutput(matches, matches.Count));
    }

    public Task<GetBlockAttributesOutput> GetBlockAttributesAsync(GetBlockAttributesInput input, CancellationToken ct = default)
    {
        var block = FindBlockOrThrow(input.Handle);
        var attributes = block.Attributes
            .Select(a => new BlockAttribute(a.Tag, a.Value, string.IsNullOrEmpty(a.Value)))
            .ToList();
        return Task.FromResult(new GetBlockAttributesOutput(block.Handle, block.BlockName, attributes));
    }

    public Task<SetBlockAttributeOutput> SetBlockAttributeAsync(SetBlockAttributeInput input, CancellationToken ct = default)
    {
        var block = FindBlockOrThrow(input.Handle);
        var attribute = block.Attributes.FirstOrDefault(a => a.Tag == input.Tag)
            ?? throw new BricscadBridgeException(BridgeErrorCode.TagNotFound, $"L'attributo '{input.Tag}' non esiste su questo blocco.");

        var previous = attribute.Value;
        attribute.Value = input.Value;
        return Task.FromResult(new SetBlockAttributeOutput(input.Handle, input.Tag, previous, input.Value, true));
    }

    public Task<GetTitleblockDataOutput> GetTitleblockDataAsync(GetTitleblockDataInput input, CancellationToken ct = default)
    {
        if (drawing.TitleblockHandle is null)
        {
            throw new BricscadBridgeException(BridgeErrorCode.TitleblockNotFound);
        }

        var block = drawing.Blocks.FirstOrDefault(b => b.Handle == drawing.TitleblockHandle)
            ?? throw new BricscadBridgeException(BridgeErrorCode.TitleblockNotFound);

        if (input.Layout is not null && block.Layout != input.Layout)
        {
            throw new BricscadBridgeException(BridgeErrorCode.LayoutNotFound);
        }

        var fields = drawing.TitleblockRequiredFields
            .Select(def =>
            {
                var value = block.Attributes.FirstOrDefault(a => a.Tag == def.Tag)?.Value ?? string.Empty;
                return new TitleblockField(def.Tag, value, def.Required, string.IsNullOrEmpty(value));
            })
            .ToList();

        var missing = fields.Where(f => f.Required && f.IsEmpty).Select(f => f.Tag).ToList();

        return Task.FromResult(new GetTitleblockDataOutput(block.Handle, block.Layout, fields, missing));
    }

    public Task<ListLayersOutput> ListLayersAsync(ListLayersInput input, CancellationToken ct = default) =>
        Task.FromResult(new ListLayersOutput(drawing.Layers));

    public Task<GetEntitiesByLayerOutput> GetEntitiesByLayerAsync(GetEntitiesByLayerInput input, CancellationToken ct = default)
    {
        if (drawing.Layers.All(l => l.Name != input.Layer))
        {
            throw new BricscadBridgeException(BridgeErrorCode.LayerNotFound);
        }

        var entities = drawing.Blocks
            .Where(b => b.Layer == input.Layer)
            .Where(b => input.EntityType is null || input.EntityType == "INSERT")
            .Select(b => new EntityRef(b.Handle, "INSERT", b.Layer))
            .ToList();

        return Task.FromResult(new GetEntitiesByLayerOutput(entities, entities.Count));
    }

    public Task<ListEntityClustersOutput> ListEntityClustersAsync(ListEntityClustersInput input, CancellationToken ct = default)
    {
        var candidates = drawing.LooseGeometry
            .Where(e => BoundingBoxIntersects(e.BoundingBox, input.BoundingBox))
            .ToList();

        var clusters = EntityClustering.Cluster(candidates, clusteringGapTolerance, input.MaxClusterExtent);
        return Task.FromResult(new ListEntityClustersOutput(clusters));
    }

    public Task<GetGeometrySignatureOutput> GetGeometrySignatureAsync(GetGeometrySignatureInput input, CancellationToken ct = default)
    {
        if (input.Handles.Count == 0)
        {
            throw new BricscadBridgeException(BridgeErrorCode.EmptySelection);
        }

        var entities = input.Handles
            .Select(h => drawing.LooseGeometry.FirstOrDefault(e => e.Handle == h)
                ?? throw new BricscadBridgeException(BridgeErrorCode.HandleNotFound, $"Handle '{h}' non trovato."))
            .ToList();

        var signature = ShapeSignature.Compute(entities);
        return Task.FromResult(new GetGeometrySignatureOutput(signature, input.Handles));
    }

    public Task<FindSimilarGeometryOutput> FindSimilarGeometryAsync(FindSimilarGeometryInput input, CancellationToken ct = default)
    {
        var boundingBoxFilter = input.BoundingBox;
        var candidateEntities = drawing.LooseGeometry
            .Where(e => boundingBoxFilter is null || BoundingBoxIntersects(e.BoundingBox, boundingBoxFilter))
            .ToList();

        var clusters = EntityClustering.Cluster(candidateEntities, clusteringGapTolerance);
        var candidates = clusters.Select(c =>
        {
            var clusterEntities = c.Handles.Select(h => drawing.LooseGeometry.First(e => e.Handle == h)).ToList();
            return (c.Handles, c.BoundingBox, Signature: ShapeSignature.Compute(clusterEntities));
        });

        var matches = SimilarityMatcher.FindSimilar(input.Signature, candidates, input.Tolerance);
        return Task.FromResult(new FindSimilarGeometryOutput(matches));
    }

    public Task<SaveSignatureAsCategoryOutput> SaveSignatureAsCategoryAsync(SaveSignatureAsCategoryInput input, CancellationToken ct = default) =>
        symbolLibrary.SaveAsync(input, ct);

    public Task<ListLearnedCategoriesOutput> ListLearnedCategoriesAsync(ListLearnedCategoriesInput input, CancellationToken ct = default) =>
        symbolLibrary.ListAsync(ct);

    public Task<GetTextInRegionOutput> GetTextInRegionAsync(GetTextInRegionInput input, CancellationToken ct = default)
    {
        var texts = drawing.TextEntities
            .Where(t => PointInBoundingBox(t.Position, input.BoundingBox))
            .Select(t => new TextEntity(t.Handle, t.Content, t.Position))
            .ToList();
        return Task.FromResult(new GetTextInRegionOutput(texts));
    }

    public Task<GetRegionScreenshotOutput> GetRegionScreenshotAsync(GetRegionScreenshotInput input, CancellationToken ct = default) =>
        Task.FromResult(new GetRegionScreenshotOutput(PlaceholderPngBase64, "image/png", input.BoundingBox));

    public Task<ExportCurrentViewImageOutput> ExportCurrentViewImageAsync(ExportCurrentViewImageInput input, CancellationToken ct = default) =>
        Task.FromResult(new ExportCurrentViewImageOutput(PlaceholderPngBase64, "image/png"));

    public Task<HighlightEntitiesOutput> HighlightEntitiesAsync(HighlightEntitiesInput input, CancellationToken ct = default)
    {
        var notFound = input.Handles.Where(h => !drawing.HandleExists(h)).ToList();
        var highlighted = input.Handles.Count - notFound.Count;
        return Task.FromResult(new HighlightEntitiesOutput(highlighted, notFound));
    }

    public Task<ZoomToOutput> ZoomToAsync(ZoomToInput input, CancellationToken ct = default)
    {
        if (!drawing.HandleExists(input.Handle))
        {
            throw new BricscadBridgeException(BridgeErrorCode.HandleNotFound);
        }

        return Task.FromResult(new ZoomToOutput(true));
    }

    public Task<RunLispOutput> RunLispAsync(RunLispInput input, CancellationToken ct = default)
    {
        if (!LispExecutionPolicy.IsAllowed(input.Expression, out var reason))
        {
            return Task.FromResult(new RunLispOutput(false, null, reason, true));
        }

        return Task.FromResult(new RunLispOutput(
            true,
            "[simulato] espressione accettata dalla policy; l'esecuzione reale richiede il bridge BricsCAD.",
            null,
            false));
    }

    private SimulatedBlockInstance FindBlockOrThrow(string handle) =>
        drawing.Blocks.FirstOrDefault(b => b.Handle == handle)
        ?? throw new BricscadBridgeException(BridgeErrorCode.HandleNotFound, $"Handle '{handle}' non trovato.");

    private void EnsureHandlesExist(IReadOnlyList<string> handles)
    {
        var missing = handles.FirstOrDefault(h => !drawing.HandleExists(h));
        if (missing is not null)
        {
            throw new BricscadBridgeException(BridgeErrorCode.HandleNotFound, $"Handle '{missing}' non trovato.");
        }
    }

    private static bool BoundingBoxIntersects(BoundingBox a, BoundingBox b) =>
        a.Min.X <= b.Max.X && a.Max.X >= b.Min.X && a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y;

    private static bool PointInBoundingBox(Point2D p, BoundingBox box) =>
        p.X >= box.Min.X && p.X <= box.Max.X && p.Y >= box.Min.Y && p.Y <= box.Max.Y;
}
