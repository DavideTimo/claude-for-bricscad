using ClaudeBridge.Core.Bridge.Models;

namespace ClaudeBridge.GeometryEngine.Models;

/// <summary>Geometria di supporto per archi: bounding box e punto medio, con angoli in radianti.</summary>
internal static class ArcMath
{
    public static Point2D PointAt(Point2D center, double radius, double angle) =>
        new(center.X + (radius * Math.Cos(angle)), center.Y + (radius * Math.Sin(angle)));

    public static double NormalizeAngle(double angle)
    {
        var twoPi = 2 * Math.PI;
        var normalized = angle % twoPi;
        return normalized < 0 ? normalized + twoPi : normalized;
    }

    private static bool AngleWithinArc(double angle, double start, double end)
    {
        angle = NormalizeAngle(angle);
        start = NormalizeAngle(start);
        end = NormalizeAngle(end);
        return start <= end ? angle >= start && angle <= end : angle >= start || angle <= end;
    }

    public static BoundingBox ComputeBoundingBox(Point2D center, double radius, double startAngle, double endAngle)
    {
        var candidates = new List<Point2D> { PointAt(center, radius, startAngle), PointAt(center, radius, endAngle) };
        foreach (var axisAngle in new[] { 0.0, Math.PI / 2, Math.PI, 3 * Math.PI / 2 })
        {
            if (AngleWithinArc(axisAngle, startAngle, endAngle))
            {
                candidates.Add(PointAt(center, radius, axisAngle));
            }
        }

        var minX = candidates.Min(p => p.X);
        var minY = candidates.Min(p => p.Y);
        var maxX = candidates.Max(p => p.X);
        var maxY = candidates.Max(p => p.Y);
        return new BoundingBox(new Point2D(minX, minY), new Point2D(maxX, maxY));
    }

    public static Point2D Midpoint(Point2D center, double radius, double startAngle, double endAngle)
    {
        var span = NormalizeAngle(endAngle - startAngle);
        return PointAt(center, radius, startAngle + (span / 2));
    }
}
