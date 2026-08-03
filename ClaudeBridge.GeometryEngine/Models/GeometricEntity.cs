using ClaudeBridge.Core.Bridge.Models;

namespace ClaudeBridge.GeometryEngine.Models;

/// <summary>
/// Rappresentazione grezza di un'entità geometrica (linea, arco, cerchio, polilinea), così come
/// letta dal bridge BricsCAD. È l'input del motore di riconoscimento: non compare mai nel Tool
/// Contract, che espone solo dati derivati (cluster, firme) — non geometria grezza a Claude.
/// </summary>
public abstract record GeometricEntity(string Handle)
{
    public abstract BoundingBox BoundingBox { get; }

    public abstract Point2D Centroid { get; }
}

public sealed record LineEntity(string Handle, Point2D Start, Point2D End) : GeometricEntity(Handle)
{
    public override BoundingBox BoundingBox => new(
        new Point2D(Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y)),
        new Point2D(Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y)));

    public override Point2D Centroid => new((Start.X + End.X) / 2, (Start.Y + End.Y) / 2);
}

public sealed record CircleEntity(string Handle, Point2D Center, double Radius) : GeometricEntity(Handle)
{
    public override BoundingBox BoundingBox => new(
        new Point2D(Center.X - Radius, Center.Y - Radius),
        new Point2D(Center.X + Radius, Center.Y + Radius));

    public override Point2D Centroid => Center;
}

/// <summary>Angoli in radianti, misurati come in BricsCAD/AutoCAD: antiorario a partire dall'asse X.</summary>
public sealed record ArcEntity(string Handle, Point2D Center, double Radius, double StartAngle, double EndAngle)
    : GeometricEntity(Handle)
{
    public override BoundingBox BoundingBox => ArcMath.ComputeBoundingBox(Center, Radius, StartAngle, EndAngle);

    public override Point2D Centroid => ArcMath.Midpoint(Center, Radius, StartAngle, EndAngle);
}

public sealed record PolylineEntity(string Handle, IReadOnlyList<Point2D> Vertices, bool Closed = false)
    : GeometricEntity(Handle)
{
    public override BoundingBox BoundingBox => new(
        new Point2D(Vertices.Min(v => v.X), Vertices.Min(v => v.Y)),
        new Point2D(Vertices.Max(v => v.X), Vertices.Max(v => v.Y)));

    public override Point2D Centroid => new(Vertices.Average(v => v.X), Vertices.Average(v => v.Y));
}
