using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.GeometryEngine.Models;

namespace ClaudeBridge.Tests.GeometryEngine;

/// <summary>
/// Genera gruppi di entità sintetiche (nessuna dipendenza da BricsCAD) per testare clustering e
/// firma geometrica: una forma "a telecamera" (cerchio + due linee + arco) riproducibile con
/// traslazione/rotazione/scala arbitrarie, e una forma chiaramente diversa (quadrato) per i
/// confronti negativi.
/// </summary>
internal static class SyntheticSymbols
{
    public static IReadOnlyList<GeometricEntity> CameraLike(
        string handlePrefix, Point2D origin, double rotation = 0, double scale = 1)
    {
        Point2D Tf(double x, double y)
        {
            var sx = x * scale;
            var sy = y * scale;
            var rx = (sx * Math.Cos(rotation)) - (sy * Math.Sin(rotation));
            var ry = (sx * Math.Sin(rotation)) + (sy * Math.Cos(rotation));
            return new Point2D(origin.X + rx, origin.Y + ry);
        }

        var center = Tf(0, 0);

        // Forma volutamente asimmetrica (non simmetrica rispetto al proprio asse principale):
        // una telecamera reale non è comunque perfettamente simmetrica, e la disambiguazione
        // rotazionale della firma (ShapeSignature) richiede un segnale di asimmetria per
        // risolvere l'ambiguità a 180° intrinseca all'asse principale PCA.
        return
        [
            new CircleEntity($"{handlePrefix}-circle", center, 1.0 * scale), // lente
            new LineEntity($"{handlePrefix}-mount", Tf(1.4, 0), Tf(2.4, 0)), // staffa, solo a destra
            new LineEntity($"{handlePrefix}-leg", Tf(0, -1.4), Tf(0, -2.4)), // gamba, solo in basso
            new ArcEntity($"{handlePrefix}-cap", center, 1.6 * scale, DegToRad(100) + rotation, DegToRad(170) + rotation), // cappuccio, in alto a sinistra
        ];
    }

    private static double DegToRad(double degrees) => degrees * Math.PI / 180.0;

    public static IReadOnlyList<GeometricEntity> SquareLike(string handlePrefix, Point2D origin)
    {
        var p1 = new Point2D(origin.X - 1, origin.Y - 1);
        var p2 = new Point2D(origin.X + 1, origin.Y - 1);
        var p3 = new Point2D(origin.X + 1, origin.Y + 1);
        var p4 = new Point2D(origin.X - 1, origin.Y + 1);

        return
        [
            new LineEntity($"{handlePrefix}-1", p1, p2),
            new LineEntity($"{handlePrefix}-2", p2, p3),
            new LineEntity($"{handlePrefix}-3", p3, p4),
            new LineEntity($"{handlePrefix}-4", p4, p1),
        ];
    }
}
