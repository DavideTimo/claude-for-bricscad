using System.Text.Json;
using ClaudeBridge.Core.Bridge;
using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.Core.Orchestration;
using ClaudeBridge.Tests.Fakes;

namespace ClaudeBridge.Tests.Orchestration;

public class ClaudeOrchestratorTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static string ReadErrorCode(ToolResultBlock result) =>
        result.Content.GetProperty("error_code").GetString()!;

    // Piano di Test §1: "Il ciclo gestisce correttamente una risposta di Claude con una singola tool call."
    [Fact]
    public async Task SendUserMessage_SingleToolCall_ReturnsFinalText()
    {
        var bridge = new FakeBricscadBridge
        {
            OnListLayers = _ => Task.FromResult(new ListLayersOutput([new LayerInfo("0", false, false, "white")])),
        };
        var client = new ScriptedAnthropicClient(
            new AssistantTurn([new ToolUseBlock("tu1", "list_layers", Parse("{}"))], "tool_use"),
            new AssistantTurn([new TextBlock("C'è un solo layer: 0.")], "end_turn"));
        var orchestrator = new ClaudeOrchestrator(client, bridge);

        var response = await orchestrator.SendUserMessageAsync("elenca i layer");

        Assert.Equal("C'è un solo layer: 0.", response);
        Assert.Equal(1, bridge.CallCount);
    }

    // Piano di Test §1: "Il ciclo gestisce correttamente più tool call in sequenza nello stesso turno."
    [Fact]
    public async Task SendUserMessage_MultipleToolCallsInSameTurn_ExecutesAllAndPairsResults()
    {
        var bridge = new FakeBricscadBridge
        {
            OnListLayers = _ => Task.FromResult(new ListLayersOutput([])),
            OnListBlockDefinitions = _ => Task.FromResult(new ListBlockDefinitionsOutput([])),
        };
        var client = new ScriptedAnthropicClient(
            new AssistantTurn(
                [
                    new ToolUseBlock("tu1", "list_layers", Parse("{}")),
                    new ToolUseBlock("tu2", "list_block_definitions", Parse("""{ "space": "any" }""")),
                ],
                "tool_use"),
            new AssistantTurn([new TextBlock("Fatto.")], "end_turn"));
        var orchestrator = new ClaudeOrchestrator(client, bridge);

        await orchestrator.SendUserMessageAsync("controlla tutto");

        Assert.Equal(2, bridge.CallCount);
        var toolResultMessage = orchestrator.History.Single(m =>
            m.Role == Role.User && m.Content.OfType<ToolResultBlock>().Any());
        var resultIds = toolResultMessage.Content.OfType<ToolResultBlock>().Select(r => r.ToolUseId).ToList();
        Assert.Equal(["tu1", "tu2"], resultIds);
    }

    // Piano di Test §1: "Un tool con mode: write non viene mai eseguito senza flag di conferma esplicito."
    [Fact]
    public async Task ExecuteToolCall_WriteToolWithoutConfirmation_NeverReachesBridge()
    {
        var bridge = new FakeBricscadBridge(); // OnSetBlockAttribute non configurato di proposito
        var client = new ScriptedAnthropicClient();
        var orchestrator = new ClaudeOrchestrator(client, bridge); // nessun confirmWrite passato

        var toolUse = new ToolUseBlock(
            "tu1", "set_block_attribute", Parse("""{ "handle": "2A4F", "tag": "TAG", "value": "OK" }"""));
        var result = await orchestrator.ExecuteToolCallAsync(toolUse);

        Assert.True(result.IsError);
        Assert.Equal("WRITE_REJECTED_BY_USER", ReadErrorCode(result));
        Assert.Equal(0, bridge.CallCount);
    }

    [Fact]
    public async Task ExecuteToolCall_WriteToolWithConfirmation_ReachesBridge()
    {
        var bridge = new FakeBricscadBridge
        {
            OnSetBlockAttribute = input => Task.FromResult(
                new SetBlockAttributeOutput(input.Handle, input.Tag, "OLD", input.Value, true)),
        };
        var client = new ScriptedAnthropicClient();
        var orchestrator = new ClaudeOrchestrator(client, bridge, confirmWrite: (_, _) => Task.FromResult(true));

        var toolUse = new ToolUseBlock(
            "tu1", "set_block_attribute", Parse("""{ "handle": "2A4F", "tag": "TAG", "value": "OK" }"""));
        var result = await orchestrator.ExecuteToolCallAsync(toolUse);

        Assert.False(result.IsError);
        Assert.Equal(1, bridge.CallCount);
    }

    // Piano di Test §1: "Parametri non conformi allo schema producono SCHEMA_VALIDATION_FAILED senza chiamare il bridge."
    [Fact]
    public async Task ExecuteToolCall_InvalidSchema_ReturnsErrorWithoutCallingBridge()
    {
        var bridge = new FakeBricscadBridge(); // OnGetBlockAttributes non configurato
        var orchestrator = new ClaudeOrchestrator(new ScriptedAnthropicClient(), bridge);

        var toolUse = new ToolUseBlock("tu1", "get_block_attributes", Parse("{}"));
        var result = await orchestrator.ExecuteToolCallAsync(toolUse);

        Assert.True(result.IsError);
        Assert.Equal("SCHEMA_VALIDATION_FAILED", ReadErrorCode(result));
        Assert.Equal(0, bridge.CallCount);
    }

    // Piano di Test §1: "Un tool sconosciuto richiesto da Claude produce TOOL_NOT_FOUND gestito, non un'eccezione non catturata."
    [Fact]
    public async Task ExecuteToolCall_UnknownTool_ReturnsToolNotFoundWithoutThrowing()
    {
        var orchestrator = new ClaudeOrchestrator(new ScriptedAnthropicClient(), new FakeBricscadBridge());

        var toolUse = new ToolUseBlock("tu1", "delete_everything", Parse("{}"));
        var result = await orchestrator.ExecuteToolCallAsync(toolUse);

        Assert.True(result.IsError);
        Assert.Equal("TOOL_NOT_FOUND", ReadErrorCode(result));
    }

    // Piano di Test §1: "Il bridge irraggiungibile (BRIDGE_UNAVAILABLE) viene comunicato a Claude
    // come risultato del tool, non come crash dell'app."
    [Fact]
    public async Task ExecuteToolCall_BridgeUnavailable_ReturnsErrorWithoutThrowing()
    {
        var bridge = new FakeBricscadBridge
        {
            OnListLayers = _ => throw new BridgeUnavailableException(),
        };
        var orchestrator = new ClaudeOrchestrator(new ScriptedAnthropicClient(), bridge);

        var toolUse = new ToolUseBlock("tu1", "list_layers", Parse("{}"));
        var result = await orchestrator.ExecuteToolCallAsync(toolUse);

        Assert.True(result.IsError);
        Assert.Equal("BRIDGE_UNAVAILABLE", ReadErrorCode(result));
    }

    [Fact]
    public async Task ExecuteToolCall_ApplicativeBridgeError_ReturnsMappedErrorCode()
    {
        var bridge = new FakeBricscadBridge
        {
            OnFindBlocks = _ => throw new BricscadBridgeException(BridgeErrorCode.InvalidPattern),
        };
        var orchestrator = new ClaudeOrchestrator(new ScriptedAnthropicClient(), bridge);

        var toolUse = new ToolUseBlock("tu1", "find_blocks", Parse("""{ "name_pattern": "" }"""));
        var result = await orchestrator.ExecuteToolCallAsync(toolUse);

        Assert.True(result.IsError);
        Assert.Equal("INVALID_PATTERN", ReadErrorCode(result));
    }

    [Fact]
    public async Task ExecuteToolCall_UnexpectedException_ReturnsInternalErrorWithoutLeakingDetails()
    {
        var bridge = new FakeBricscadBridge
        {
            OnListLayers = _ => throw new InvalidOperationException("stack trace with sensitive path C:\\secret"),
        };
        var orchestrator = new ClaudeOrchestrator(new ScriptedAnthropicClient(), bridge);

        var toolUse = new ToolUseBlock("tu1", "list_layers", Parse("{}"));
        var result = await orchestrator.ExecuteToolCallAsync(toolUse);

        Assert.True(result.IsError);
        Assert.Equal("INTERNAL_ERROR", ReadErrorCode(result));
        Assert.DoesNotContain("secret", result.Content.GetProperty("message").GetString());
    }
}
