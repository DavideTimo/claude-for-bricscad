using System.Net;
using System.Text.Json;
using ClaudeBridge.Core.Orchestration;
using ClaudeBridge.Tests.Fakes;

namespace ClaudeBridge.Tests.Orchestration;

public class AnthropicClientTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static AnthropicClient CreateClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), apiKey: "test-key", model: "claude-test-model");

    [Fact]
    public async Task SendAsync_TextOnlyResponse_ReturnsTextBlockAndEndTurn()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """
            { "content": [ { "type": "text", "text": "Ciao!" } ], "stop_reason": "end_turn" }
            """);
        var client = CreateClient(handler);
        var history = new List<ConversationMessage> { new(Role.User, [new TextBlock("ciao")]) };

        var turn = await client.SendAsync(history);

        Assert.Equal("end_turn", turn.StopReason);
        Assert.False(turn.RequestsToolUse);
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(turn.Content));
        Assert.Equal("Ciao!", textBlock.Text);
    }

    [Fact]
    public async Task SendAsync_ToolUseResponse_ReturnsToolUseBlock()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """
            {
              "content": [ { "type": "tool_use", "id": "toolu_1", "name": "list_layers", "input": {} } ],
              "stop_reason": "tool_use"
            }
            """);
        var client = CreateClient(handler);
        var history = new List<ConversationMessage> { new(Role.User, [new TextBlock("elenca i layer")]) };

        var turn = await client.SendAsync(history);

        Assert.True(turn.RequestsToolUse);
        var toolUse = Assert.IsType<ToolUseBlock>(Assert.Single(turn.Content));
        Assert.Equal("toolu_1", toolUse.Id);
        Assert.Equal("list_layers", toolUse.Name);
        Assert.Equal(JsonValueKind.Object, toolUse.Input.ValueKind);
    }

    [Fact]
    public async Task SendAsync_SetsApiKeyAndVersionHeaders()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """
            { "content": [], "stop_reason": "end_turn" }
            """);
        var client = new AnthropicClient(new HttpClient(handler), apiKey: "sk-ant-secret", model: "claude-test-model");

        await client.SendAsync([new ConversationMessage(Role.User, [new TextBlock("ciao")])]);

        var request = handler.Requests.Single().Request;
        Assert.Equal("sk-ant-secret", request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());
    }

    [Fact]
    public async Task SendAsync_IncludesFullToolCatalogInRequest()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """
            { "content": [], "stop_reason": "end_turn" }
            """);
        var client = CreateClient(handler);

        await client.SendAsync([new ConversationMessage(Role.User, [new TextBlock("ciao")])]);

        var body = Parse(handler.Requests.Single().Body);
        var toolNames = body.GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString())
            .ToList();

        Assert.Equal(Core.Tools.ToolCatalog.All.Count, toolNames.Count);
        Assert.Contains("find_blocks", toolNames);
        Assert.Contains("run_lisp", toolNames);
    }

    // Piano di Test §1: "Serializzazione corretta della cronologia conversazione su più turni."
    [Fact]
    public async Task SendAsync_MultiTurnHistory_SerializesRolesAndContentCorrectly()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """
            { "content": [ { "type": "text", "text": "ok" } ], "stop_reason": "end_turn" }
            """);
        var client = CreateClient(handler);

        var toolOutput = Parse("""{ "layers": [] }""");
        var history = new List<ConversationMessage>
        {
            new(Role.User, [new TextBlock("controlla i layer")]),
            new(Role.Assistant, [new ToolUseBlock("tu1", "list_layers", Parse("{}"))]),
            new(Role.User, [new ToolResultBlock("tu1", toolOutput, IsError: false)]),
        };

        await client.SendAsync(history);

        var body = Parse(handler.Requests.Single().Body);
        var messages = body.GetProperty("messages");
        Assert.Equal(3, messages.GetArrayLength());

        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("text", messages[0].GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Equal("controlla i layer", messages[0].GetProperty("content")[0].GetProperty("text").GetString());

        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        var toolUseJson = messages[1].GetProperty("content")[0];
        Assert.Equal("tool_use", toolUseJson.GetProperty("type").GetString());
        Assert.Equal("tu1", toolUseJson.GetProperty("id").GetString());
        Assert.Equal("list_layers", toolUseJson.GetProperty("name").GetString());

        Assert.Equal("user", messages[2].GetProperty("role").GetString());
        var toolResultJson = messages[2].GetProperty("content")[0];
        Assert.Equal("tool_result", toolResultJson.GetProperty("type").GetString());
        Assert.Equal("tu1", toolResultJson.GetProperty("tool_use_id").GetString());
        Assert.False(toolResultJson.GetProperty("is_error").GetBoolean());
        Assert.Equal(JsonValueKind.String, toolResultJson.GetProperty("content").ValueKind);
        var embedded = JsonDocument.Parse(toolResultJson.GetProperty("content").GetString()!).RootElement;
        Assert.Equal(0, embedded.GetProperty("layers").GetArrayLength());
    }

    // Piano di Test §1: "Gestione corretta di un errore HTTP (rate limit, chiave non valida)
    // senza far cadere l'intera sessione di chat."
    [Fact]
    public async Task SendAsync_RateLimitError_ThrowsTypedExceptionWithDetails()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.TooManyRequests, """
            { "type": "error", "error": { "type": "rate_limit_error", "message": "Rate limit exceeded" } }
            """);
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<AnthropicApiException>(() =>
            client.SendAsync([new ConversationMessage(Role.User, [new TextBlock("ciao")])]));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Equal("rate_limit_error", ex.ErrorType);
        Assert.Equal("Rate limit exceeded", ex.Message);
    }

    [Fact]
    public async Task SendAsync_InvalidApiKey_ThrowsTypedExceptionWithDetails()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.Unauthorized, """
            { "type": "error", "error": { "type": "authentication_error", "message": "invalid x-api-key" } }
            """);
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<AnthropicApiException>(() =>
            client.SendAsync([new ConversationMessage(Role.User, [new TextBlock("ciao")])]));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal("authentication_error", ex.ErrorType);
    }

    [Fact]
    public async Task SendAsync_NonJsonErrorBody_ThrowsTypedExceptionWithGenericMessage()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("Bad Gateway"),
        });
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<AnthropicApiException>(() =>
            client.SendAsync([new ConversationMessage(Role.User, [new TextBlock("ciao")])]));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Null(ex.ErrorType);
    }
}
