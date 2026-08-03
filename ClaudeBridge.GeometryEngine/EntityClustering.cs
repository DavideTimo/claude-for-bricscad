using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.GeometryEngine.Models;

namespace ClaudeBridge.GeometryEngine;

/// <summary>
/// Raggruppa entità geometriche spazialmente vicine/connesse in candidati "simbolo" (Tool
/// Contract, list_entity_clusters). Le soglie (gapTolerance, maxClusterExtent) sono parametri
/// espliciti, non default nascosti: la taratura corretta richiede verifica su disegni reali
/// (Riconoscimento simboli, §3 — rischio tecnico noto, non assunto corretto a priori).
/// </summary>
public static class EntityClustering
{
    public static IReadOnlyList<EntityCluster> Cluster(
        IReadOnlyList<GeometricEntity> entities,
        double gapTolerance,
        double? maxClusterExtent = null)
    {
        if (entities.Count == 0)
        {
            return [];
        }

        var parent = Enumerable.Range(0, entities.Count).ToArray();

        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        void Union(int a, int b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB)
            {
                parent[rootA] = rootB;
            }
        }

        for (var i = 0; i < entities.Count; i++)
        {
            for (var j = i + 1; j < entities.Count; j++)
            {
                if (GeometryMath.Gap(entities[i].BoundingBox, entities[j].BoundingBox) <= gapTolerance)
                {
                    Union(i, j);
                }
            }
        }

        var clusters = new List<EntityCluster>();
        var clusterIndex = 0;

        foreach (var group in Enumerable.Range(0, entities.Count).GroupBy(Find))
        {
            var members = group.Select(i => entities[i]).ToList();
            var box = GeometryMath.Combine(members.Select(e => e.BoundingBox));
            var extent = Math.Max(box.Max.X - box.Min.X, box.Max.Y - box.Min.Y);

            if (maxClusterExtent is { } max && extent > max)
            {
                continue; // elemento strutturale (parete, quotatura...), non candidato simbolo
            }

            clusters.Add(new EntityCluster(
                ClusterId: $"cluster-{clusterIndex++}",
                Handles: members.Select(e => e.Handle).ToList(),
                BoundingBox: box,
                EntityCount: members.Count,
                IsBlockInstance: false));
        }

        return clusters;
    }
}
