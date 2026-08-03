using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.GeometryEngine.Models;

namespace ClaudeBridge.GeometryEngine;

/// <summary>
/// Confronta firme geometriche e cerca match per similarità (Tool Contract, find_similar_geometry).
/// I pesi combinati sono un punto di partenza da tarare sui disegni reali, non valori definitivi
/// (coerente con la nota sulle soglie in "System prompt di riferimento", §Note di implementazione).
/// </summary>
public static class SimilarityMatcher
{
    public static double Compare(GeometrySignature a, GeometrySignature b)
    {
        var primitiveScore = ComparePrimitiveCounts(a.PrimitiveCounts, b.PrimitiveCounts);
        var aspectScore = CompareAspectRatio(a.BoundingBoxAspectRatio, b.BoundingBoxAspectRatio);
        var pointScore = ComparePoints(ShapeSignature.Deserialize(a.RelativeGeometry), ShapeSignature.Deserialize(b.RelativeGeometry));

        var score = (0.3 * primitiveScore) + (0.2 * aspectScore) + (0.5 * pointScore);
        return Math.Clamp(score, 0.0, 1.0);
    }

    /// <summary>Cerca, tra i candidati, quelli con similarità alla firma target sopra la soglia data.</summary>
    public static IReadOnlyList<GeometryMatch> FindSimilar(
        GeometrySignature target,
        IEnumerable<(IReadOnlyList<string> Handles, BoundingBox BoundingBox, GeometrySignature Signature)> candidates,
        double tolerance = 0.85) =>
        candidates
            .Select(c => new GeometryMatch(c.Handles, c.BoundingBox, Math.Round(Compare(target, c.Signature), 4)))
            .Where(m => m.SimilarityScore >= tolerance)
            .OrderByDescending(m => m.SimilarityScore)
            .ToList();

    private static double ComparePrimitiveCounts(PrimitiveCounts a, PrimitiveCounts b)
    {
        var diff = Math.Abs(a.Line - b.Line) + Math.Abs(a.Arc - b.Arc)
            + Math.Abs(a.Circle - b.Circle) + Math.Abs(a.Polyline - b.Polyline);
        var total = a.Line + a.Arc + a.Circle + a.Polyline + b.Line + b.Arc + b.Circle + b.Polyline;
        return total == 0 ? 1.0 : Math.Clamp(1.0 - ((double)diff / total), 0.0, 1.0);
    }

    private static double CompareAspectRatio(double a, double b)
    {
        var denom = Math.Max(Math.Max(a, b), GeometryMath.Epsilon);
        return Math.Clamp(1.0 - (Math.Abs(a - b) / denom), 0.0, 1.0);
    }

    private static double ComparePoints(IReadOnlyList<RelativePoint> a, IReadOnlyList<RelativePoint> b)
    {
        if (a.Count == 0 && b.Count == 0)
        {
            return 1.0;
        }

        if (a.Count != b.Count)
        {
            return 0.0;
        }

        // Entrambe le liste sono già ordinate canonicamente da ShapeSignature (kind, distanza,
        // angolo): per forme uguali o quasi uguali l'accoppiamento per indice è già quello giusto.
        var totalDistance = 0.0;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].Kind != b[i].Kind)
            {
                return 0.0;
            }

            var distanceDelta = a[i].NormalizedDistance - b[i].NormalizedDistance;
            var angleDelta = GeometryMath.NormalizeAngleSigned(a[i].RelativeAngle - b[i].RelativeAngle);
            totalDistance += Math.Sqrt((distanceDelta * distanceDelta) + (angleDelta * angleDelta));
        }

        var avgDistance = totalDistance / a.Count;
        return 1.0 / (1.0 + avgDistance);
    }
}
