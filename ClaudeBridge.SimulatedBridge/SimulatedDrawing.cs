using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.GeometryEngine.Models;

namespace ClaudeBridge.SimulatedBridge;

/// <summary>
/// Rappresentazione in memoria di un disegno, usata al posto di un vero DWG/BricsCAD. Permette
/// di far girare l'intero ciclo di tool-use (ClaudeOrchestrator + ToolDispatcher + GeometryEngine)
/// senza BricsCAD — per demo, test di integrazione, e sviluppo della UI prima che il plugin reale
/// esista.
/// </summary>
public sealed class SimulatedDrawing
{
    public List<SimulatedBlockInstance> Blocks { get; } = [];

    public List<LayerInfo> Layers { get; } = [];

    /// <summary>Geometria non a blocco (candidati per il matching geometrico, Livello 2).</summary>
    public List<GeometricEntity> LooseGeometry { get; } = [];

    public List<SimulatedTextEntity> TextEntities { get; } = [];

    public string? TitleblockHandle { get; set; }

    public List<TitleblockFieldDefinition> TitleblockRequiredFields { get; } = [];

    private readonly Dictionary<string, TaggedEntity> _tags = [];

    public IReadOnlyDictionary<string, TaggedEntity> Tags => _tags;

    public void Tag(string handle, string label, DateTimeOffset taggedAt) =>
        _tags[handle] = new TaggedEntity(handle, label, taggedAt.ToString("O"));

    /// <summary>Ogni handle noto nel disegno (blocchi, geometria sciolta, testo), per validare gli input dei tool.</summary>
    public bool HandleExists(string handle) =>
        Blocks.Any(b => b.Handle == handle)
        || LooseGeometry.Any(e => e.Handle == handle)
        || TextEntities.Any(t => t.Handle == handle);

    public string? LayerOf(string handle) =>
        Blocks.FirstOrDefault(b => b.Handle == handle)?.Layer;
}

public sealed record SimulatedBlockInstance(
    string Handle,
    string BlockName,
    string Layer,
    Point3D InsertionPoint,
    string Layout,
    List<SimulatedAttribute> Attributes);

public sealed class SimulatedAttribute(string tag, string value)
{
    public string Tag { get; } = tag;

    public string Value { get; set; } = value;
}

public sealed record SimulatedTextEntity(string Handle, string Content, Point2D Position, string Layout);

public sealed record TitleblockFieldDefinition(string Tag, bool Required);
