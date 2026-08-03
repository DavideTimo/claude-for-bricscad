namespace ClaudeBridge.Core.Bridge.Models;

public sealed record ListLayersInput;

public sealed record LayerInfo(string Name, bool IsFrozen, bool IsLocked, string Color);

public sealed record ListLayersOutput(IReadOnlyList<LayerInfo> Layers);

public sealed record GetEntitiesByLayerInput(string Layer, string? EntityType = null);

public sealed record EntityRef(string Handle, string Type, string Layer);

public sealed record GetEntitiesByLayerOutput(IReadOnlyList<EntityRef> Entities, int Count);
