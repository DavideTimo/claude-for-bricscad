using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.GeometryEngine;
using ClaudeBridge.GeometryEngine.Models;

namespace ClaudeBridge.Tests.GeometryEngine;

public class EntityClusteringTests
{
    // Piano di Test §1: "due gruppi di entità chiaramente separati nello spazio producono due cluster distinti."
    [Fact]
    public void Cluster_TwoSeparatedGroups_ProducesTwoDistinctClusters()
    {
        var groupA = SyntheticSymbols.CameraLike("a", new Point2D(0, 0));
        var groupB = SyntheticSymbols.CameraLike("b", new Point2D(1000, 1000));

        var clusters = EntityClustering.Cluster([.. groupA, .. groupB], gapTolerance: 0.5);

        Assert.Equal(2, clusters.Count);
        Assert.All(clusters, c => Assert.Equal(4, c.EntityCount));
    }

    // Piano di Test §1: "entità di un singolo simbolo (vicine/connesse) producono un solo cluster, non frammentato."
    [Fact]
    public void Cluster_EntitiesOfSingleSymbol_ProducesOneClusterNotFragmented()
    {
        var symbol = SyntheticSymbols.CameraLike("s", new Point2D(0, 0));

        var clusters = EntityClustering.Cluster(symbol, gapTolerance: 0.5);

        var cluster = Assert.Single(clusters);
        Assert.Equal(4, cluster.EntityCount);
        Assert.Equal(symbol.Select(e => e.Handle).OrderBy(h => h), cluster.Handles.OrderBy(h => h));
    }

    // Coerente con Piano di Test §1 Scenario K: una soglia troppo stretta spezza il simbolo in più cluster.
    [Fact]
    public void Cluster_WithTooTightTolerance_FragmentsSingleSymbol()
    {
        var symbol = SyntheticSymbols.CameraLike("s", new Point2D(0, 0));

        var clusters = EntityClustering.Cluster(symbol, gapTolerance: 0.1);

        Assert.True(clusters.Count > 1, "A too-tight gap tolerance is expected to fragment the symbol.");
    }

    [Fact]
    public void Cluster_ElementExceedingMaxExtent_IsExcludedAsStructural()
    {
        var wall = new List<GeometricEntity> { new LineEntity("wall", new Point2D(0, 0), new Point2D(500, 0)) };

        var clusters = EntityClustering.Cluster(wall, gapTolerance: 0.5, maxClusterExtent: 50);

        Assert.Empty(clusters);
    }

    // Rischio tecnico noto (Riconoscimento simboli, §3 / Piano di Test, Scenario K): un simbolo
    // a ridosso di un elemento strutturale viene unito ad esso dal clustering per prossimità; il
    // filtro per estensione massima scarta l'intero cluster risultante, perdendo anche il
    // simbolo. Va tarato sui disegni reali, non risolto qui in astratto — questo test documenta
    // il comportamento attuale, non lo presenta come corretto.
    [Fact]
    public void Cluster_SymbolTouchingStructuralElement_MergesAndIsExcludedByExtentFilter()
    {
        var wall = new List<GeometricEntity> { new LineEntity("wall", new Point2D(-100, -1), new Point2D(400, -1)) };
        var symbol = SyntheticSymbols.CameraLike("cam", new Point2D(0, 0));

        var clusters = EntityClustering.Cluster([.. wall, .. symbol], gapTolerance: 0.5, maxClusterExtent: 50);

        Assert.Empty(clusters);
    }

    [Fact]
    public void Cluster_EmptyInput_ReturnsNoClusters()
    {
        var clusters = EntityClustering.Cluster([], gapTolerance: 0.5);

        Assert.Empty(clusters);
    }
}
