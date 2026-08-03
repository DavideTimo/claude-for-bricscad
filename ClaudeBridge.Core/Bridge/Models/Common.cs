namespace ClaudeBridge.Core.Bridge.Models;

public sealed record Point2D(double X, double Y);

public sealed record Point3D(double X, double Y, double Z);

public sealed record BoundingBox(Point2D Min, Point2D Max);
