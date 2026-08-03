using System.Text.Json;

namespace ClaudeBridge.Core.Orchestration;

public abstract record ContentBlock;

public sealed record TextBlock(string Text) : ContentBlock;

public sealed record ToolUseBlock(string Id, string Name, JsonElement Input) : ContentBlock;

public sealed record ToolResultBlock(string ToolUseId, JsonElement Content, bool IsError) : ContentBlock;
