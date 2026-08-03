using ClaudeBridge.Core.Bridge.Models;

namespace ClaudeBridge.GeometryEngine.Models;

internal static class GeometryMath
{
    public const double Epsilon = 1e-9;

    public static BoundingBox Combine(IEnumerable<BoundingBox> boxes)
    {
        var list = boxes as IReadOnlyList<BoundingBox> ?? boxes.ToList();
        return new BoundingBox(
            new Point2D(list.Min(b => b.Min.X), list.Min(b => b.Min.Y)),
            new Point2D(list.Max(b => b.Max.X), list.Max(b => b.Max.Y)));
    }

    public static double Distance(Point2D a, Point2D b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    public static double Angle(Point2D from, Point2D to) => Math.Atan2(to.Y - from.Y, to.X - from.X);

    /// <summary>Distanza minima tra due bounding box assiali; 0 se sovrapposte o a contatto.</summary>
    public static double Gap(BoundingBox a, BoundingBox b)
    {
        var dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
        var dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// Angolo dell'asse principale (PCA su un set di punti 2D): la direzione di massima varianza,
    /// usata come riferimento a rotazione zero per rendere la firma geometrica rotation-invariant.
    /// </summary>
    public static double PrincipalAxisAngle(IReadOnlyList<Point2D> points, Point2D centroid)
    {
        double sxx = 0, syy = 0, sxy = 0;
        foreach (var p in points)
        {
            var dx = p.X - centroid.X;
            var dy = p.Y - centroid.Y;
            sxx += dx * dx;
            syy += dy * dy;
            sxy += dx * dy;
        }

        if (Math.Abs(sxx) < Epsilon && Math.Abs(syy) < Epsilon && Math.Abs(sxy) < Epsilon)
        {
            return 0.0; // punti coincidenti: nessuna varianza, l'orientamento non è definito
        }

        return 0.5 * Math.Atan2(2 * sxy, sxx - syy);
    }

    public static double NormalizeAngleSigned(double angle)
    {
        var twoPi = 2 * Math.PI;
        var normalized = angle % twoPi;
        if (normalized > Math.PI)
        {
            normalized -= twoPi;
        }
        else if (normalized < -Math.PI)
        {
            normalized += twoPi;
        }

        return normalized;
    }
}
