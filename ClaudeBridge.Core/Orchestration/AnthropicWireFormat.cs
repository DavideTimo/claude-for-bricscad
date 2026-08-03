using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeBridge.Core.Tools;

namespace ClaudeBridge.Core.Orchestration;

/// <summary>
/// Traduzione tra i modelli di conversazione di ClaudeOrchestrator e il formato JSON della
/// Messages API (blocchi "text" / "tool_use" / "tool_result", tool "input_schema").
/// </summary>
internal static class AnthropicWireFormat
{
    public static JsonObject BuildRequestBody(
        string model,
        int maxTokens,
        string? systemPrompt,
        IReadOnlyList<ConversationMessage> history)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["messages"] = SerializeMessages(history),
            ["tools"] = SerializeTools(),
        };

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            body["system"] = systemPrompt;
        }

        return body;
    }

    private static JsonArray SerializeTools()
    {
        var tools = new JsonArray();
        foreach (var spec in ToolCatalog.All)
        {
            tools.Add(new JsonObject
            {
                ["name"] = spec.Name,
                ["description"] = spec.Description,
                ["input_schema"] = ToolCatalog.BuildInputSchema(spec),
            });
        }

        return tools;
    }

    private static JsonArray SerializeMessages(IReadOnlyList<ConversationMessage> history)
    {
        var messages = new JsonArray();
        foreach (var message in history)
        {
            messages.Add(new JsonObject
            {
                ["role"] = message.Role == Role.User ? "user" : "assistant",
                ["content"] = SerializeContent(message.Content),
            });
        }

        return messages;
    }

    private static JsonArray SerializeContent(IReadOnlyList<ContentBlock> blocks)
    {
        var array = new JsonArray();
        foreach (var block in blocks)
        {
            array.Add(SerializeBlock(block));
        }

        return array;
    }

    private static JsonObject SerializeBlock(ContentBlock block) => block switch
    {
        TextBlock text => new JsonObject { ["type"] = "text", ["text"] = text.Text },
        ToolUseBlock toolUse => new JsonObject
        {
            ["type"] = "tool_use",
            ["id"] = toolUse.Id,
            ["name"] = toolUse.Name,
            ["input"] = JsonNode.Parse(toolUse.Input.GetRawText()),
        },
        ToolResultBlock toolResult => new JsonObject
        {
            ["type"] = "tool_result",
            ["tool_use_id"] = toolResult.ToolUseId,
            ["content"] = toolResult.Content.GetRawText(),
            ["is_error"] = toolResult.IsError,
        },
        _ => throw new NotSupportedException($"Unsupported content block: {block.GetType().Name}"),
    };

    public static AssistantTurn ParseAssistantTurn(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var stopReason = root.TryGetProperty("stop_reason", out var stopReasonElement)
            ? stopReasonElement.GetString() ?? "end_turn"
            : "end_turn";

        var blocks = new List<ContentBlock>();
        foreach (var block in root.GetProperty("content").EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            switch (type)
            {
                case "text":
                    blocks.Add(new TextBlock(block.GetProperty("text").GetString() ?? string.Empty));
                    break;
                case "tool_use":
                    blocks.Add(new ToolUseBlock(
                        block.GetProperty("id").GetString()!,
                        block.GetProperty("name").GetString()!,
                        block.GetProperty("input").Clone()));
                    break;
                default:
                    // Blocchi non gestiti (es. futuri tipi introdotti dall'API) vengono ignorati
                    // invece di far fallire l'intero turno.
                    break;
            }
        }

        return new AssistantTurn(blocks, stopReason);
    }
}
