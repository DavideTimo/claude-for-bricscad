using System.Globalization;
using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.GeometryEngine.Models;

namespace ClaudeBridge.GeometryEngine;

/// <summary>
/// Estrae una firma geometrica invariante a scala/rotazione da un gruppo di entità (Tool
/// Contract, get_geometry_signature). Il campo RelativeGeometry della firma è "opaco a Claude"
/// per contratto: qui è un formato interno che SimilarityMatcher sa reinterpretare, non
/// un dettaglio da esporre o spiegare all'utente.
/// </summary>
public static class ShapeSignature
{
    public static GeometrySignature Compute(IReadOnlyList<GeometricEntity> entities)
    {
        if (entities.Count == 0)
        {
            throw new ArgumentException("Cannot compute a signature for an empty entity set.", nameof(entities));
        }

        var counts = CountPrimitives(entities);
        var centroids = entities.Select(e => e.Centroid).ToList();
        var centroid = new Point2D(centroids.Average(p => p.X), centroids.Average(p => p.Y));

        // L'asse principale (PCA) è definito solo a meno di 180°: la formula si basa sui momenti
        // del secondo ordine, che non distinguono una configurazione dalla sua rotazione di π.
        // Il momento del terzo ordine (skewness) rompe questa ambiguità per forme non simmetriche
        // rispetto al proprio asse principale — senza, la firma non sarebbe davvero
        // rotation-invariant (due rotazioni diverse della stessa forma potrebbero produrre
        // riferimenti angolari opposti, facendo crollare artificialmente la similarità).
        var rawAxisAngle = GeometryMath.PrincipalAxisAngle(centroids, centroid);
        var axisAngle = DisambiguateAxis(rawAxisAngle, ThirdMomentVector(centroids, centroid));

        var overallBox = GeometryMath.Combine(entities.Select(e => e.BoundingBox));
        var scale = Math.Max(GeometryMath.Distance(overallBox.Min, overallBox.Max), GeometryMath.Epsilon);

        var aspect = RotatedAspectRatio(entities, centroid, axisAngle);

        var relativePoints = entities
            .Select(e => new RelativePoint(
                KindCode(e),
                GeometryMath.Distance(e.Centroid, centroid) / scale,
                GeometryMath.NormalizeAngleSigned(GeometryMath.Angle(centroid, e.Centroid) - axisAngle)))
            .OrderBy(p => p.Kind, StringComparer.Ordinal)
            .ThenBy(p => Math.Round(p.NormalizedDistance, 4))
            .ThenBy(p => Math.Round(p.RelativeAngle, 4))
            .ToList();

        return new GeometrySignature(counts, Serialize(relativePoints), Math.Round(aspect, 4));
    }

    private static PrimitiveCounts CountPrimitives(IReadOnlyList<GeometricEntity> entities) => new(
        Line: entities.OfType<LineEntity>().Count(),
        Arc: entities.OfType<ArcEntity>().Count(),
        Circle: entities.OfType<CircleEntity>().Count(),
        Polyline: entities.OfType<PolylineEntity>().Count());

    private static string KindCode(GeometricEntity entity) => entity switch
    {
        LineEntity => "L",
        ArcEntity => "A",
        CircleEntity => "C",
        PolylineEntity => "P",
        _ => throw new NotSupportedException($"Unsupported entity type: {entity.GetType().Name}"),
    };

    /// <summary>Somma pesata per distanza² dei vettori centroide→punto: nullo per forme simmetriche
    /// rispetto al proprio asse principale, altrimenti punta in una direzione rotation-covariant
    /// (non affetta dall'ambiguità a meno di π del solo secondo ordine).</summary>
    private static Point2D ThirdMomentVector(IReadOnlyList<Point2D> points, Point2D centroid)
    {
        double vx = 0, vy = 0;
        foreach (var p in points)
        {
            var dx = p.X - centroid.X;
            var dy = p.Y - centroid.Y;
            var distSquared = (dx * dx) + (dy * dy);
            vx += distSquared * dx;
            vy += distSquared * dy;
        }

        return new Point2D(vx, vy);
    }

    private static double DisambiguateAxis(double rawAxisAngle, Point2D thirdMomentVector)
    {
        if (Math.Abs(thirdMomentVector.X) < GeometryMath.Epsilon && Math.Abs(thirdMomentVector.Y) < GeometryMath.Epsilon)
        {
            return rawAxisAngle; // forma simmetrica rispetto al proprio asse: nessun segnale per disambiguare
        }

        var skewAngle = Math.Atan2(thirdMomentVector.Y, thirdMomentVector.X);
        var diffToRaw = Math.Abs(GeometryMath.NormalizeAngleSigned(skewAngle - rawAxisAngle));
        var diffToFlipped = Math.Abs(GeometryMath.NormalizeAngleSigned(skewAngle - (rawAxisAngle + Math.PI)));
        return diffToFlipped < diffToRaw ? rawAxisAngle + Math.PI : rawAxisAngle;
    }

    private static double RotatedAspectRatio(IReadOnlyList<GeometricEntity> entities, Point2D centroid, double axisAngle)
    {
        var cos = Math.Cos(-axisAngle);
        var sin = Math.Sin(-axisAngle);

        var corners = entities.SelectMany(e =>
        {
            var box = e.BoundingBox;
            return new[]
            {
                box.Min, box.Max,
                new Point2D(box.Min.X, box.Max.Y),
                new Point2D(box.Max.X, box.Min.Y),
            };
        });

        double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
        foreach (var p in corners)
        {
            var dx = p.X - centroid.X;
            var dy = p.Y - centroid.Y;
            var u = (dx * cos) - (dy * sin);
            var v = (dx * sin) + (dy * cos);
            minU = Math.Min(minU, u);
            maxU = Math.Max(maxU, u);
            minV = Math.Min(minV, v);
            maxV = Math.Max(maxV, v);
        }

        var width = Math.Max(maxU - minU, GeometryMath.Epsilon);
        var height = Math.Max(maxV - minV, GeometryMath.Epsilon);
        return Math.Max(width, height) / Math.Max(Math.Min(width, height), GeometryMath.Epsilon);
    }

    internal static string Serialize(IReadOnlyList<RelativePoint> points) => string.Join(
        '|',
        points.Select(p => string.Create(
            CultureInfo.InvariantCulture,
            $"{p.Kind}:{p.NormalizedDistance:F4}:{p.RelativeAngle:F4}")));

    internal static IReadOnlyList<RelativePoint> Deserialize(string relativeGeometry)
    {
        if (string.IsNullOrEmpty(relativeGeometry))
        {
            return [];
        }

        return relativeGeometry
            .Split('|')
            .Select(token =>
            {
                var parts = token.Split(':');
                return new RelativePoint(
                    parts[0],
                    double.Parse(parts[1], CultureInfo.InvariantCulture),
                    double.Parse(parts[2], CultureInfo.InvariantCulture));
            })
            .ToList();
    }
}

internal sealed record RelativePoint(string Kind, double NormalizedDistance, double RelativeAngle);
