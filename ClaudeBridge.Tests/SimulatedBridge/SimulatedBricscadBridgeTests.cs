using ClaudeBridge.Core.Bridge;
using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.GeometryEngine;
using ClaudeBridge.SimulatedBridge;

namespace ClaudeBridge.Tests.SimulatedBridgeTests;

public class SimulatedBricscadBridgeTests
{
    private static SimulatedBricscadBridge CreateBridge(out SimulatedDrawing drawing)
    {
        drawing = SampleDrawings.BuildHeterogeneousDrawing();
        return new SimulatedBricscadBridge(drawing, new SymbolLibrary(), clusteringGapTolerance: 0.5);
    }

    [Fact]
    public async Task ListBlockDefinitions_GroupsByNameWithCorrectCounts()
    {
        var bridge = CreateBridge(out _);

        var result = await bridge.ListBlockDefinitionsAsync(new ListBlockDefinitionsInput());

        Assert.Equal(12, result.BlockDefinitions.Single(d => d.Name == "mncfasoijds").InstanceCount);
        Assert.Equal(3, result.BlockDefinitions.Single(d => d.Name == "A$C4E2F1B0").InstanceCount);
    }

    [Fact]
    public async Task FindBlocks_WithNoMatchingPattern_ReturnsEmptyNotAnError()
    {
        var bridge = CreateBridge(out _);

        var result = await bridge.FindBlocksAsync(new FindBlocksInput("TELECAMERA*"));

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public async Task FindBlocks_EmptyPattern_ThrowsInvalidPattern()
    {
        var bridge = CreateBridge(out _);

        var ex = await Assert.ThrowsAsync<BricscadBridgeException>(() =>
            bridge.FindBlocksAsync(new FindBlocksInput("")));

        Assert.Equal(BridgeErrorCode.InvalidPattern, ex.Code);
    }

    [Fact]
    public async Task FindBlocks_WildcardPattern_MatchesCaseInsensitively()
    {
        var bridge = CreateBridge(out _);

        var result = await bridge.FindBlocksAsync(new FindBlocksInput("MNCFAS*"));

        Assert.Equal(12, result.Count);
    }

    [Fact]
    public async Task GetBlockAttributes_UnknownHandle_ThrowsHandleNotFound()
    {
        var bridge = CreateBridge(out _);

        var ex = await Assert.ThrowsAsync<BricscadBridgeException>(() =>
            bridge.GetBlockAttributesAsync(new GetBlockAttributesInput("does-not-exist")));

        Assert.Equal(BridgeErrorCode.HandleNotFound, ex.Code);
    }

    [Fact]
    public async Task GetTitleblockData_ReportsOnlyEmptyRequiredFieldAsMissing()
    {
        var bridge = CreateBridge(out _);

        var result = await bridge.GetTitleblockDataAsync(new GetTitleblockDataInput());

        Assert.Equal(["DATA"], result.MissingRequiredFields);
    }

    [Fact]
    public async Task TagAndFindTaggedEntities_RoundTrips()
    {
        var bridge = CreateBridge(out var drawing);
        var handles = drawing.LooseGeometry.Where(e => e.Handle.StartsWith("cam0")).Select(e => e.Handle).ToList();

        await bridge.TagEntitiesAsCategoryAsync(new TagEntitiesAsCategoryInput(handles, "telecamera"));
        var result = await bridge.FindTaggedEntitiesAsync(new FindTaggedEntitiesInput("telecamera"));

        Assert.Equal(handles.Count, result.TaggedEntities.Count);
    }

    [Fact]
    public async Task TagEntitiesAsCategory_UnknownHandle_ThrowsHandleNotFound()
    {
        var bridge = CreateBridge(out _);

        var ex = await Assert.ThrowsAsync<BricscadBridgeException>(() =>
            bridge.TagEntitiesAsCategoryAsync(new TagEntitiesAsCategoryInput(["ghost"], "telecamera")));

        Assert.Equal(BridgeErrorCode.HandleNotFound, ex.Code);
    }

    [Fact]
    public async Task ListEntityClusters_RestrictedToRegion_FindsOnlyThatCamera()
    {
        var bridge = CreateBridge(out _);
        var region = new BoundingBox(new Point2D(-10, -10), new Point2D(10, 10));

        var result = await bridge.ListEntityClustersAsync(new ListEntityClustersInput(region));

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(4, cluster.EntityCount);
    }

    // Riproduce, tramite il bridge, il flusso "insegna per esempio" (Riconoscimento simboli §2):
    // firma su un esempio noto, poi ricerca per similarità sull'intero disegno.
    [Fact]
    public async Task GetGeometrySignatureThenFindSimilarGeometry_FindsOtherCamerasNotTheSquare()
    {
        var bridge = CreateBridge(out var drawing);
        var exampleHandles = drawing.LooseGeometry.Where(e => e.Handle.StartsWith("cam0")).Select(e => e.Handle).ToList();

        var signatureResult = await bridge.GetGeometrySignatureAsync(new GetGeometrySignatureInput(exampleHandles));
        var matches = await bridge.FindSimilarGeometryAsync(
            new FindSimilarGeometryInput(signatureResult.Signature, Tolerance: 0.85));

        // 5 telecamere totali: l'esempio stesso (score 1.0) + le altre 4 (di cui una più incerta,
        // scalata 1.3x) — nessuna deve includere il quadrato "noise".
        Assert.True(matches.Matches.Count is >= 4 and <= 5, $"Expected 4-5 camera matches, got {matches.Matches.Count}.");
        Assert.DoesNotContain(matches.Matches, m => m.Handles.Any(h => h.StartsWith("noise")));
    }

    [Fact]
    public async Task GetGeometrySignature_EmptyHandles_ThrowsEmptySelection()
    {
        var bridge = CreateBridge(out _);

        var ex = await Assert.ThrowsAsync<BricscadBridgeException>(() =>
            bridge.GetGeometrySignatureAsync(new GetGeometrySignatureInput([])));

        Assert.Equal(BridgeErrorCode.EmptySelection, ex.Code);
    }

    [Fact]
    public async Task RunLisp_DangerousExpression_IsBlockedByPolicyNotThrown()
    {
        var bridge = CreateBridge(out _);

        var result = await bridge.RunLispAsync(new RunLispInput("(startapp \"cmd.exe\")", "test"));

        Assert.False(result.Success);
        Assert.True(result.BlockedByPolicy);
    }

    [Fact]
    public async Task RunLisp_LegitimateExpression_Succeeds()
    {
        var bridge = CreateBridge(out _);

        var result = await bridge.RunLispAsync(new RunLispInput("(command \"_ZOOM\" \"_E\")", "adatta la vista"));

        Assert.True(result.Success);
        Assert.False(result.BlockedByPolicy);
    }

    [Fact]
    public async Task HighlightEntities_ReportsNotFoundHandlesSeparately()
    {
        var bridge = CreateBridge(out var drawing);
        var realHandle = drawing.LooseGeometry.First().Handle;

        var result = await bridge.HighlightEntitiesAsync(new HighlightEntitiesInput([realHandle, "ghost"]));

        Assert.Equal(1, result.HighlightedCount);
        Assert.Equal(["ghost"], result.NotFoundHandles);
    }
}
