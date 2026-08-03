namespace ClaudeBridge.Core.Bridge.Models;

public sealed record FindTaggedEntitiesInput(string? Label = null);

public sealed record TaggedEntity(string Handle, string Label, string TaggedAt);

public sealed record FindTaggedEntitiesOutput(IReadOnlyList<TaggedEntity> TaggedEntities);

public sealed record TagEntitiesAsCategoryInput(IReadOnlyList<string> Handles, string Label);

public sealed record TagEntitiesAsCategoryOutput(int TaggedCount, string Label);
