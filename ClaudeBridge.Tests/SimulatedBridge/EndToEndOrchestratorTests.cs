using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.Core.Json;
using ClaudeBridge.Core.Orchestration;
using ClaudeBridge.GeometryEngine;
using ClaudeBridge.SimulatedBridge;
using ClaudeBridge.Tests.Fakes;

namespace ClaudeBridge.Tests.SimulatedBridgeTests;

/// <summary>
/// Riproduce, con un client Anthropic scriptato al posto di una vera chiamata API, la traccia
/// del documento "Esempio end-to-end tracciato": nessun tag/nome utile (Livelli 0-1), esempio
/// indicato dall'utente, firma e ricerca per similarità (Livello 2), evidenziazione e tag
/// persistente. A differenza dei test unitari su Core/GeometryEngine/SimulatedBridge presi
/// singolarmente, questo verifica che i tre componenti funzionino correttamente insieme,
/// attraverso il ciclo di tool-use reale di ClaudeOrchestrator.
/// </summary>
public class EndToEndOrchestratorTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement ToJsonElement(JsonObject obj) => JsonDocument.Parse(obj.ToJsonString()).RootElement;

    [Fact]
    public async Task TeachByExampleFlow_EndsWithTaggedCamerasAndNoTagsOnNoise()
    {
        var drawing = SampleDrawings.BuildHeterogeneousDrawing();
        var bridge = new SimulatedBricscadBridge(drawing, new SymbolLibrary(), clusteringGapTolerance: 0.5);

        var camera0Handles = drawing.LooseGeometry
            .Where(e => e.Handle.StartsWith("cam0"))
            .Select(e => e.Handle)
            .ToList();
        var camera0Entities = drawing.LooseGeometry.Where(e => camera0Handles.Contains(e.Handle)).ToList();
        var camera0Signature = ShapeSignature.Compute(camera0Entities);
        var camera0SignatureJson = JsonSerializer.SerializeToElement(camera0Signature, ToolJsonOptions.Default);

        var allCameraHandles = drawing.LooseGeometry
            .Where(e => e.Handle.StartsWith("cam"))
            .Select(e => e.Handle)
            .ToList();

        // Turno 1 (doc09): Livello 0 fallisce, Livello 1 fallisce (nomi generati automaticamente),
        // nessuna categoria appresa — Claude chiede un esempio, nessun tool_use nell'ultimo turno.
        var findTagged = new ToolUseBlock("tu1", "find_tagged_entities", Parse("""{ "label": "telecamera" }"""));
        var listBlocks = new ToolUseBlock("tu2", "list_block_definitions", Parse("""{ "space": "any" }"""));
        var listLearned = new ToolUseBlock("tu3", "list_learned_categories", Parse("{}"));

        // Turno 2: l'utente indica l'esempio (handle noti) — Claude calcola la firma e cerca match simili.
        var getSignatureInput = ToJsonElement(new JsonObject { ["handles"] = new JsonArray(camera0Handles.Select(h => (JsonNode)h).ToArray()) });
        var getSignature = new ToolUseBlock("tu4", "get_geometry_signature", getSignatureInput);

        var findSimilarInput = ToJsonElement(new JsonObject
        {
            ["signature"] = JsonNode.Parse(camera0SignatureJson.GetRawText()),
            ["tolerance"] = 0.6,
            ["space"] = "any",
        });
        var findSimilar = new ToolUseBlock("tu5", "find_similar_geometry", findSimilarInput);

        // Turno 3: evidenzia e persiste il riconoscimento (tag_entities_as_category è "write").
        var highlightInput = ToJsonElement(new JsonObject { ["handles"] = new JsonArray(allCameraHandles.Select(h => (JsonNode)h).ToArray()) });
        var highlight = new ToolUseBlock("tu6", "highlight_entities", highlightInput);

        var tagInput = ToJsonElement(new JsonObject
        {
            ["handles"] = new JsonArray(allCameraHandles.Select(h => (JsonNode)h).ToArray()),
            ["label"] = "telecamera",
        });
        var tag = new ToolUseBlock("tu7", "tag_entities_as_category", tagInput);

        var client = new ScriptedAnthropicClient(
            new AssistantTurn([findTagged], "tool_use"),
            new AssistantTurn([listBlocks], "tool_use"),
            new AssistantTurn([listLearned], "tool_use"),
            new AssistantTurn([new TextBlock("Puoi indicarmi un esempio di telecamera nel disegno?")], "end_turn"),
            new AssistantTurn([getSignature], "tool_use"),
            new AssistantTurn([findSimilar], "tool_use"),
            new AssistantTurn([new TextBlock("Ho trovato altre telecamere simili. Vuoi che le evidenzi?")], "end_turn"),
            new AssistantTurn([highlight], "tool_use"),
            new AssistantTurn([tag], "tool_use"),
            new AssistantTurn([new TextBlock("Fatto — evidenziate e taggate.")], "end_turn"));

        var orchestrator = new ClaudeOrchestrator(client, bridge, confirmWrite: (_, _) => Task.FromResult(true));

        var reply1 = await orchestrator.SendUserMessageAsync("controlla se ho messo il tag a ogni telecamera");
        Assert.Contains("esempio", reply1, StringComparison.OrdinalIgnoreCase);

        var reply2 = await orchestrator.SendUserMessageAsync($"eccola: {string.Join(",", camera0Handles)}");
        Assert.Contains("telecamere", reply2, StringComparison.OrdinalIgnoreCase);

        var reply3 = await orchestrator.SendUserMessageAsync("sì, evidenziale e salva");
        Assert.Contains("taggate", reply3, StringComparison.OrdinalIgnoreCase);

        // Nessun ToolResultBlock nella cronologia deve essere un errore: prova che l'intera
        // catena Core → GeometryEngine → SimulatedBridge ha funzionato senza intoppi.
        var toolResults = orchestrator.History
            .SelectMany(m => m.Content.OfType<ToolResultBlock>())
            .ToList();
        Assert.NotEmpty(toolResults);
        Assert.DoesNotContain(toolResults, r => r.IsError);

        // find_similar_geometry deve aver trovato le altre telecamere ma non il rumore.
        var findSimilarResult = toolResults.Single(r => r.ToolUseId == "tu5");
        var matchedHandles = findSimilarResult.Content.GetProperty("matches").EnumerateArray()
            .SelectMany(m => m.GetProperty("handles").EnumerateArray().Select(h => h.GetString()))
            .ToList();
        Assert.True(matchedHandles.Count >= 4, $"Expected at least 4 matched camera entities, got {matchedHandles.Count}.");
        Assert.DoesNotContain(matchedHandles, h => h!.StartsWith("noise"));

        // Il tag persistente deve coprire tutte le telecamere effettivamente evidenziate.
        var finalTags = await bridge.FindTaggedEntitiesAsync(new FindTaggedEntitiesInput("telecamera"));
        Assert.Equal(allCameraHandles.Count, finalTags.TaggedEntities.Count);
        Assert.DoesNotContain(finalTags.TaggedEntities, t => t.Handle.StartsWith("noise"));
    }

    [Fact]
    public async Task WriteToolWithoutConfirmation_NeverPersistsTagInSimulatedDrawing()
    {
        var drawing = SampleDrawings.BuildHeterogeneousDrawing();
        var bridge = new SimulatedBricscadBridge(drawing, new SymbolLibrary());
        var handle = drawing.LooseGeometry.First().Handle;

        var tagInput = ToJsonElement(new JsonObject
        {
            ["handles"] = new JsonArray((JsonNode)handle),
            ["label"] = "telecamera",
        });
        var client = new ScriptedAnthropicClient(
            new AssistantTurn([new ToolUseBlock("tu1", "tag_entities_as_category", tagInput)], "tool_use"),
            new AssistantTurn([new TextBlock("Annullato.")], "end_turn"));

        // Nessun confirmWrite passato: il gate di conferma nega di default (ClaudeOrchestrator).
        var orchestrator = new ClaudeOrchestrator(client, bridge);
        await orchestrator.SendUserMessageAsync("tagga questa entità come telecamera");

        var tags = await bridge.FindTaggedEntitiesAsync(new FindTaggedEntitiesInput());
        Assert.Empty(tags.TaggedEntities);
    }
}
