namespace ClaudeBridge.Core.Bridge.Models;

public sealed record ListBlockDefinitionsInput(string Space = "any");

public sealed record BlockDefinition(
    string Name,
    int InstanceCount,
    IReadOnlyList<string> AttributeTags,
    string SampleLayer);

public sealed record ListBlockDefinitionsOutput(IReadOnlyList<BlockDefinition> BlockDefinitions);

public sealed record FindBlocksInput(string NamePattern, string? Layer = null, string Space = "any");

public sealed record BlockMatch(
    string Handle,
    string BlockName,
    string Layer,
    Point3D InsertionPoint,
    string Layout,
    bool HasAttributes);

public sealed record FindBlocksOutput(IReadOnlyList<BlockMatch> Matches, int Count);

public sealed record GetBlockAttributesInput(string Handle);

public sealed record BlockAttribute(string Tag, string Value, bool IsEmpty);

public sealed record GetBlockAttributesOutput(
    string Handle,
    string BlockName,
    IReadOnlyList<BlockAttribute> Attributes);

public sealed record SetBlockAttributeInput(string Handle, string Tag, string Value);

public sealed record SetBlockAttributeOutput(
    string Handle,
    string Tag,
    string PreviousValue,
    string NewValue,
    bool Success);

public sealed record GetTitleblockDataInput(string? Layout = null);

public sealed record TitleblockField(string Tag, string Value, bool Required, bool IsEmpty);

public sealed record GetTitleblockDataOutput(
    string Handle,
    string Layout,
    IReadOnlyList<TitleblockField> Fields,
    IReadOnlyList<string> MissingRequiredFields);
